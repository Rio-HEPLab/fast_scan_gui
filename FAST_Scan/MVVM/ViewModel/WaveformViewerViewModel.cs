using FAST_Scan.Core;
using FAST_Scan.Core.Caen;
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

        // Várias curvas de cores diferentes, com legenda (digitizer). Se não for null, a View
        // desenha estas e ignora Frames.
        public List<WaveformTrace> Traces { get; set; }
    }

    // Uma curva rotulada (canal ou TR) do mesmo evento.
    internal class WaveformTrace
    {
        public string Label { get; set; }
        public double[] Y { get; set; }
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
        public string DigitizerChannelsText { get; set; } = "0";     // lista, por exemplo "0,1,8"
        public bool DigitizerShowTr { get; set; } = true;            // mostra o TR de cada grupo usado
        public bool DigitizerSubtractBaseline { get; set; } = true;  // só nos canais (o TR fica bruto)
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

        private string _statusText = "Ready.";
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
            StatusText = "Stopping... (waiting for the current block to finish)";
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
            uint digitizerMask = 1;
            bool showTr = DigitizerShowTr;
            bool subtractBaseline = DigitizerSubtractBaseline;
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
                string channelError;
                if (!TryParseChannels(DigitizerChannelsText, out digitizerMask, out channelError))
                {
                    Fail(channelError);
                    return;
                }
                if (!TryInt(BaselinePointsText, 1, 100000, out baselinePoints))
                {
                    Fail("Baseline points must be an integer between 1 and 100000.");
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            CountersText = "";
            StatusText = "Starting...";
            IsRunning = true;
            ScanStateManager.SetScanRunning(true);   // bloqueia troca de tela e fechamento do programa
            CommandManager.InvalidateRequerySuggested();

            Exception failure = null;
            try
            {
                if (source == WaveformSource.Oscilloscope)
                    await Task.Run(() => RunOscilloscope(scopeSettings, overlay, verbose, token));
                else
                    await Task.Run(() => RunDigitizer(digitizerMask, showTr, subtractBaseline, baselinePoints, token));
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
                StatusText = "Stopped.";
                Logger.Instance.Log("Waveform viewer stopped.", LogType.Info);
            }
        }

        private void Fail(string message)
        {
            StatusText = "Error: " + message;
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

                StatusText = "Connecting to the oscilloscope...";
                scope.Open();
                scope.Apply();

                StatusText = "Acquiring...";
                Logger.Instance.Log("Viewer: oscilloscope acquisition started.", LogType.Info);

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
                        "Blocks: {0}\nEvents: {1}\nMean rate: {2:0} events/s\nLast block: capture {3:0} ms | XZero {4:0} ms | CURVe? {5:0} ms | total transfer {6:0} ms",
                        blocks, events, events / seconds, block.CaptureMs, block.XZeroMs, block.CurveMs, block.TransferMs);

                    PublishOscilloscope(block, overlay);
                }
            }
        }

        // Converte só os últimos 'overlay' frames of the block para volts (barato) e envia à View.
        private void PublishOscilloscope(WaveformBlock block, int overlay)
        {
            int count = Math.Min(overlay, block.FrameCount);
            var frames = new List<double[]>(count);
            for (int f = block.FrameCount - count; f < block.FrameCount; f++)
                frames.Add(block.GetFrameVolts(f));

            Raise(new WaveformDisplay
            {
                Title = "Oscilloscope: last " + count + " frames of the block",
                XLabel = "Time relative to trigger (ns)",
                YLabel = "Voltage (V)",
                X = block.GetTimeAxisNanoseconds(),
                Frames = frames
            });
        }

        // ------------------------------------------------------------------
        // Digitizer
        // ------------------------------------------------------------------
        private void RunDigitizer(uint channelMask, bool showTr, bool subtractBaseline, int baselinePoints, CancellationToken token)
        {
#if TEST_MODE
            RunDigitizerSimulated(token);
#else
            RunDigitizerReal(channelMask, showTr, subtractBaseline, baselinePoints, token);
#endif
        }

        // Modo de teste (sem placa): uma curva do gerador de waveform.
        private void RunDigitizerSimulated(CancellationToken token)
        {
            int[] buffer = new int[1024];
            double[] bins = new double[buffer.Length];
            for (int i = 0; i < bins.Length; i++) bins[i] = i;

            StatusText = "Acquiring (simulated)...";
            while (!token.IsCancellationRequested)
            {
                Digitizer.GenerateWaveform_Sim(buffer);
                Thread.Sleep(20);

                double[] samples = new double[buffer.Length];
                for (int i = 0; i < samples.Length; i++) samples[i] = buffer[i];

                Raise(new WaveformDisplay
                {
                    Title = "Digitizer (simulated): last waveform",
                    XLabel = "Bin",
                    YLabel = "Amplitude (ADC)",
                    X = bins,
                    Frames = new List<double[]> { samples }
                });
            }
        }

        // Leitura contínua: inicia uma vez, lê eventos em lotes e mostra o último evento
        // com todos os canais escolhidos (e o TR de cada grupo) no mesmo gráfico.
        private void RunDigitizerReal(uint channelMask, bool showTr, bool subtractBaseline, int baselinePoints, CancellationToken token)
        {
            const int eventsPerRead = 50;
            const int noTriggerTimeoutMs = 5000;

            Digitizer.EnsureLogHook();
            DigitizerSettings settings = DigitizerSettings.CreateDefault();
            settings.ChannelMask = channelMask;
            settings.TrDigitize = showTr;

            StatusText = "Connecting to the digitizer...";
            using (DgtzDevice device = DgtzDevice.Open(settings))
            {
                DigitizerInfo info = device.Info;
                int n = info.RecordLength;
                DigitizerEventBlock block = device.CreateBlock(eventsPerRead);

                var labels = new string[info.SlotCount];
                var isTr = new bool[info.SlotCount];
                for (int s = 0; s < info.SlotCount; s++)
                {
                    int id = info.SlotIds[s];
                    isTr[s] = id >= DigitizerInfo.TrSlotBase;
                    labels[s] = isTr[s] ? "TR group " + (id - DigitizerInfo.TrSlotBase) : "Ch " + id;
                }

                double[] time = new double[n];
                for (int i = 0; i < n; i++) time[i] = i * info.SamplePeriodNs;
                int basePoints = Math.Max(1, Math.Min(baselinePoints, n));

                device.Start();
                try
                {
                    StatusText = "Acquiring...";
                    Logger.Instance.Log("Viewer: digitizer acquisition started (" + string.Join(", ", labels) + ").", LogType.Info);

                    var clock = Stopwatch.StartNew();
                    var sinceLastEvent = Stopwatch.StartNew();
                    long events = 0;
                    bool waiting = false;

                    while (!token.IsCancellationRequested)
                    {
                        int count = device.ReadEvents(block, eventsPerRead, 200, token);
                        if (count < 1)
                        {
                            if (sinceLastEvent.ElapsedMilliseconds > noTriggerTimeoutMs)
                                throw new DigitizerException(DgtzStatus.Timeout, "No trigger received from the digitizer within " + (noTriggerTimeoutMs / 1000) + " s.");
                            if (!waiting && sinceLastEvent.ElapsedMilliseconds > 1000)
                            {
                                waiting = true;
                                StatusText = "Waiting for trigger...";
                            }
                            continue;
                        }

                        sinceLastEvent.Restart();
                        if (waiting) { waiting = false; StatusText = "Acquiring..."; }
                        events += count;

                        // Mostra só o último evento do lote.
                        int last = count - 1;
                        var traces = new List<WaveformTrace>(info.SlotCount);
                        for (int s = 0; s < info.SlotCount; s++)
                        {
                            int offset = block.OffsetOf(last, s);
                            double baseline = 0;
                            if (subtractBaseline && !isTr[s])
                            {
                                for (int i = 0; i < basePoints; i++) baseline += block.Samples[offset + i];
                                baseline /= basePoints;
                            }

                            double[] y = new double[n];
                            for (int i = 0; i < n; i++) y[i] = block.Samples[offset + i] - baseline;
                            traces.Add(new WaveformTrace { Label = labels[s], Y = y });
                        }

                        CountersText = string.Format(CultureInfo.InvariantCulture,
                            "Events: {0}\nMean rate: {1:0} events/s\nSample period: {2:0.###} ns ({3} samples)",
                            events, events / clock.Elapsed.TotalSeconds, info.SamplePeriodNs, n);

                        Raise(new WaveformDisplay
                        {
                            Title = "Digitizer: last event",
                            XLabel = "Time in the acquisition window (ns)",
                            YLabel = "Amplitude (ADC codes" + (subtractBaseline ? "; channels minus baseline" : "") + ")",
                            X = time,
                            Traces = traces
                        });
                    }
                }
                finally
                {
                    try { if (device.IsRunning) device.Stop(); } catch (DigitizerException) { }
                }
            }
        }

        // Lê uma lista de canais ("0,1,8" ou "0 1 8") e monta a máscara de bits.
        private static bool TryParseChannels(string text, out uint mask, out string error)
        {
            mask = 0;
            error = null;
            string[] parts = (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                error = "Enter at least one digitizer channel (0 to 15), for example: 0,1,8.";
                return false;
            }
            foreach (string part in parts)
            {
                int channel;
                if (!TryInt(part, 0, 15, out channel))
                {
                    error = "Invalid digitizer channel '" + part + "': use integers from 0 to 15 separated by commas.";
                    return false;
                }
                mask |= 1u << channel;
            }
            return true;
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

            if (!TryInt(ScopeChannelText, 1, 4, out channel)) { error = "Channel must be an integer from 1 to 4."; return null; }
            if (!TryInt(FrameCountText, 1, 1000000, out frames)) { error = "Frame count must be a positive integer."; return null; }
            if (!TryInt(OverlayFramesText, 1, 1000, out overlay)) { error = "Overlaid frames must be an integer between 1 and 1000."; return null; }
            if (!TryInt(RecordLengthText, 2, 100000000, out recordLength)) { error = "Points per frame must be a positive integer."; return null; }
            if (!TryDouble(SampleRateGsText, out rateGs) || rateGs <= 0) { error = "Invalid sample rate (GS/s)."; return null; }
            if (!TryDouble(VerticalScaleMvText, out scaleMv) || scaleMv <= 0) { error = "Invalid vertical scale (mV/div)."; return null; }
            if (!TryDouble(TriggerLevelMvText, out levelMv)) { error = "Invalid trigger level (mV)."; return null; }
            if (!TryDouble(TriggerPositionText, out positionPercent)) { error = "Invalid trigger position (%)."; return null; }

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
