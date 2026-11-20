using FAST_Scan.Core;
using FAST_Scan.Core.Oscilloscope;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FAST_Scan.MVVM.ViewModel
{
    internal enum WaveformSource
    {
        Oscilloscope,
        Digitizer
    }

    // Dados prontos para a View desenhar (já em unidades de exibição).
    internal class WaveformDisplay
    {
        public string Title { get; set; }
        public string XLabel { get; set; }
        public string YLabel { get; set; }
        public double[] X { get; set; }
        public List<double[]> Frames { get; set; }   // o último é destacado
    }

    // Visualizador de waveform ao vivo: o usuário escolhe a fonte (osciloscópio ou digitizer).
    // Esta classe NÃO conhece o ScottPlot: ela só entrega WaveformDisplay pelo evento
    // DisplayReady, e a View decide como desenhar.
    internal class WaveformViewerViewModel : ObservebleObject
    {
        // ---------------- Fonte ----------------
        private WaveformSource _source = WaveformSource.Oscilloscope;

        public bool IsOscilloscope
        {
            get { return _source == WaveformSource.Oscilloscope; }
            set { if (value) SetSource(WaveformSource.Oscilloscope); }
        }

        public bool IsDigitizer
        {
            get { return _source == WaveformSource.Digitizer; }
            set { if (value) SetSource(WaveformSource.Digitizer); }
        }

        private void SetSource(WaveformSource source)
        {
            _source = source;
            OnPropertyChanged("IsOscilloscope");
            OnPropertyChanged("IsDigitizer");
        }

        // ---------------- Osciloscópio (texto digitado pelo usuário) ----------------
        public string VisaResource { get; set; } = "TCPIP0::152.84.100.65::inst0::INSTR";
        public string ScopeChannelText { get; set; } = "1";
        public string FrameCountText { get; set; } = "200";
        public string OverlayFramesText { get; set; } = "30";
        public string RecordLengthText { get; set; } = "5000";
        public string SampleRateGsText { get; set; } = "100";
        public string VerticalScaleMvText { get; set; } = "30";
        public string TriggerLevelMvText { get; set; } = "-336";
        public string TriggerPositionText { get; set; } = "11";
        public bool TriggerFalling { get; set; } = true;
        public bool Termination50 { get; set; } = true;
        public bool ReadXZero { get; set; } = true;
        public bool KeepTriggerChannelOn { get; set; } = false;
        public bool VerboseLog { get; set; } = false;   // registra cada comando/resposta no terminal (diagnóstico; deixa mais lento)

        // ---------------- Digitizer ----------------
        public string DigitizerChannelText { get; set; } = "0";
        public string BaselinePointsText { get; set; } = "100";

        // ---------------- Estado e contadores ----------------
        private bool _isRunning;
        public bool IsRunning
        {
            get { return _isRunning; }
            private set
            {
                _isRunning = value;
                OnPropertyChanged();
                OnPropertyChanged("CanEdit");
            }
        }

        public bool CanEdit { get { return !_isRunning; } }

        private string _statusText = "Pronto.";
        public string StatusText { get { return _statusText; } private set { _statusText = value; OnPropertyChanged(); } }

        private string _countersText = "";
        public string CountersText { get { return _countersText; } private set { _countersText = value; OnPropertyChanged(); } }

        // ---------------- Comandos ----------------
        public RelayCommand StartCommand { get; private set; }
        public RelayCommand StopCommand { get; private set; }

        public event Action<WaveformDisplay> DisplayReady;

        private CancellationTokenSource _cts;

        public WaveformViewerViewModel()
        {
            StartCommand = new RelayCommand(async o => await StartAsync(), o => !IsRunning);
            StopCommand = new RelayCommand(o => Stop(), o => IsRunning);
        }

        private void Stop()
        {
            CancellationTokenSource cts = _cts;
            if (cts != null) cts.Cancel();
            StatusText = "Parando... (aguardando o fim do bloco atual)";
        }

        // ------------------------------------------------------------------
        // Início da aquisição
        // ------------------------------------------------------------------
        private async Task StartAsync()
        {
            if (IsRunning) return;

            // Lê e valida tudo na thread da interface, antes de iniciar.
            WaveformSource source = _source;
            ScopeSettings scopeSettings = null;
            int overlay = 1;
            bool verbose = VerboseLog;
            int digitizerChannel = 0;
            int baselinePoints = 100;

            if (source == WaveformSource.Oscilloscope)
            {
                string error;
                scopeSettings = BuildScopeSettings(out overlay, out error);
                if (scopeSettings == null)
                {
                    Fail(error);
                    return;
                }
            }
            else
            {
                if (!TryInt(DigitizerChannelText, 0, 63, out digitizerChannel) ||
                    !TryInt(BaselinePointsText, 1, 100000, out baselinePoints))
                {
                    Fail("Parâmetros do digitizer inválidos (canal e pontos de baseline devem ser inteiros).");
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            CountersText = "";
            StatusText = "Iniciando...";
            IsRunning = true;
            ScanStateManager.SetScanRunning(true);   // bloqueia troca de tela e fechamento do programa
            CommandManager.InvalidateRequerySuggested();

            Exception failure = null;
            try
            {
                if (source == WaveformSource.Oscilloscope)
                    await Task.Run(() => RunOscilloscope(scopeSettings, overlay, verbose, token));
                else
                    await Task.Run(() => RunDigitizer(digitizerChannel, baselinePoints, token));
            }
            catch (OperationCanceledException)
            {
                // parada pedida pelo usuário
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            _cts.Dispose();
            _cts = null;
            ScanStateManager.SetScanRunning(false);
            IsRunning = false;
            CommandManager.InvalidateRequerySuggested();

            if (failure != null)
                Fail(failure.Message);
            else
            {
                StatusText = "Parado.";
                Logger.Instance.Log("Visualizador de waveform parado.", LogType.Info);
            }
        }

        private void Fail(string message)
        {
            StatusText = "Erro: " + message;
            Logger.Instance.Log(message, LogType.Error);
        }

        // ------------------------------------------------------------------
        // Osciloscópio
        // ------------------------------------------------------------------
        private void RunOscilloscope(ScopeSettings settings, int overlay, bool verbose, CancellationToken token)
        {
            // using => Dispose => restaura a configuração original, mesmo em caso de erro.
            using (var scope = new TektronixScope(settings))
            {
                scope.Message += text => Logger.Instance.Log(text, LogType.Info);
                if (verbose)
                    scope.Trace += text => Logger.Instance.Log(text, LogType.Data);   // diagnóstico: cada comando e resposta

                StatusText = "Conectando ao osciloscópio...";
                scope.Open();
                scope.Apply();

                StatusText = "Adquirindo...";
                Logger.Instance.Log("Visualizador: aquisição do osciloscópio iniciada.", LogType.Info);

                var clock = Stopwatch.StartNew();
                int blocks = 0;
                long events = 0;

                while (!token.IsCancellationRequested)
                {
                    WaveformBlock block = scope.CaptureBlock(token);
                    blocks++;
                    events += block.FrameCount;

                    double seconds = clock.Elapsed.TotalSeconds;
                    CountersText = string.Format(CultureInfo.InvariantCulture,
                        "Blocos: {0}\nEventos: {1}\nTaxa média: {2:0} eventos/s\nÚltimo bloco: captura {3:0} ms | XZero {4:0} ms | CURVe? {5:0} ms | total transf. {6:0} ms",
                        blocks, events, events / seconds, block.CaptureMs, block.XZeroMs, block.CurveMs, block.TransferMs);

                    PublishOscilloscope(block, overlay);
                }
            }
        }

        // Converte só os últimos 'overlay' frames do bloco para volts (barato) e envia à View.
        private void PublishOscilloscope(WaveformBlock block, int overlay)
        {
            int count = Math.Min(overlay, block.FrameCount);
            var frames = new List<double[]>(count);
            for (int f = block.FrameCount - count; f < block.FrameCount; f++)
                frames.Add(block.GetFrameVolts(f));

            Raise(new WaveformDisplay
            {
                Title = "Osciloscópio: últimos " + count + " frames do bloco",
                XLabel = "Tempo relativo ao trigger (ns)",
                YLabel = "Tensão (V)",
                X = block.GetTimeAxisNanoseconds(),
                Frames = frames
            });
        }

        // ------------------------------------------------------------------
        // Digitizer
        // ------------------------------------------------------------------
        private void RunDigitizer(int channel, int baselinePoints, CancellationToken token)
        {
            int[] buffer = new int[1024];   // mesmo tamanho do DataStore.Waveform
            double[] bins = new double[buffer.Length];
            for (int i = 0; i < bins.Length; i++) bins[i] = i;

            Action acquireOne;
#if !TEST_MODE
            StatusText = "Configurando o digitizer...";
            if (Convert.ToBoolean(Digitizer.Configure()))
                throw new InvalidOperationException("Não foi possível configurar o digitizer.");
            acquireOne = () => Digitizer.GetWaveform(buffer, channel, baselinePoints);
#else
            acquireOne = () => { Digitizer.GenerateWaveform_Sim(buffer); Thread.Sleep(20); };
#endif
            try
            {
                StatusText = "Adquirindo...";
                Logger.Instance.Log("Visualizador: aquisição do digitizer iniciada.", LogType.Info);

                var clock = Stopwatch.StartNew();
                long waveforms = 0;

                while (!token.IsCancellationRequested)
                {
                    acquireOne();
                    waveforms++;

                    double[] samples = new double[buffer.Length];
                    for (int i = 0; i < samples.Length; i++) samples[i] = buffer[i];

                    CountersText = string.Format(CultureInfo.InvariantCulture,
                        "Waveforms: {0}\nTaxa média: {1:0.0} waveforms/s",
                        waveforms, waveforms / clock.Elapsed.TotalSeconds);

                    Raise(new WaveformDisplay
                    {
                        Title = "Digitizer: última waveform",
                        XLabel = "Bin",
                        YLabel = "Amplitude (ADC)",
                        X = bins,
                        Frames = new List<double[]> { samples }
                    });

                    Thread.Sleep(10);   // evita saturar a CPU se a leitura voltar imediatamente
                }
            }
            finally
            {
#if !TEST_MODE
                Digitizer.Close();
#endif
            }
        }

        private void Raise(WaveformDisplay display)
        {
            Action<WaveformDisplay> handler = DisplayReady;
            if (handler != null) handler(display);
        }

        // ------------------------------------------------------------------
        // Leitura e validação dos campos de texto
        // ------------------------------------------------------------------
        private ScopeSettings BuildScopeSettings(out int overlay, out string error)
        {
            overlay = 1;
            error = null;

            int channel, frames, recordLength;
            double rateGs, scaleMv, levelMv, positionPercent;

            if (!TryInt(ScopeChannelText, 1, 4, out channel)) { error = "Canal deve ser um inteiro de 1 a 4."; return null; }
            if (!TryInt(FrameCountText, 1, 1000000, out frames)) { error = "Número de frames deve ser um inteiro positivo."; return null; }
            if (!TryInt(OverlayFramesText, 1, 1000, out overlay)) { error = "Frames sobrepostos deve ser um inteiro entre 1 e 1000."; return null; }
            if (!TryInt(RecordLengthText, 2, 100000000, out recordLength)) { error = "Pontos por frame deve ser um inteiro positivo."; return null; }
            if (!TryDouble(SampleRateGsText, out rateGs) || rateGs <= 0) { error = "Taxa de amostragem inválida (GS/s)."; return null; }
            if (!TryDouble(VerticalScaleMvText, out scaleMv) || scaleMv <= 0) { error = "Escala vertical inválida (mV/div)."; return null; }
            if (!TryDouble(TriggerLevelMvText, out levelMv)) { error = "Nível do trigger inválido (mV)."; return null; }
            if (!TryDouble(TriggerPositionText, out positionPercent)) { error = "Posição do trigger inválida (%)."; return null; }

            // Não mostra mais frames do que o bloco tem.
            if (overlay > frames) overlay = frames;

            var settings = new ScopeSettings
            {
                VisaResource = (VisaResource ?? "").Trim(),
                Channel = channel,
                FrameCount = frames,
                RecordLength = recordLength,
                SampleRateHz = rateGs * 1e9,
                VerticalScaleVoltsPerDiv = scaleMv / 1000.0,
                TriggerLevelVolts = levelMv / 1000.0,
                TriggerPositionPercent = positionPercent,
                Slope = TriggerFalling ? TriggerSlope.Falling : TriggerSlope.Rising,
                Termination50Ohm = Termination50,
                ReadXZero = ReadXZero,
                KeepTriggerChannelOn = KeepTriggerChannelOn
            };

            List<string> problems = settings.Validate();
            if (problems.Count > 0)
            {
                error = string.Join(" ", problems);
                return null;
            }
            return settings;
        }

        // Aceita vírgula ou ponto decimal e notação científica (por exemplo 1e9).
        private static bool TryDouble(string text, out double value)
        {
            string normalized = (text ?? "").Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryInt(string text, int min, int max, out int value)
        {
            if (int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value >= min && value <= max;
            return false;
        }
    }
}
