using FAST_Scan.Core;
using FAST_Scan.Core.Analysis;
using FAST_Scan.Core.Oscilloscope;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FAST_Scan.MVVM.ViewModel
{
    // Dados prontos para a View desenhar (waveform de um evento + histograma).
    internal class TimeResolutionDisplay
    {
        // ----- Waveform do evento mostrado -----
        public double[] TimeNs { get; set; }
        public double[] Volts { get; set; }
        public double[] TriggerVolts { get; set; }       // null se o canal do trigger não foi digitalizado
        public double LevelVolts { get; set; } = double.NaN;
        public double BaselineVolts { get; set; } = double.NaN;
        public double BaselineStartNs { get; set; }
        public double BaselineEndNs { get; set; }
        public double CrossingNs { get; set; } = double.NaN;
        public double TriggerCrossingNs { get; set; } = double.NaN;
        public double PeakNs { get; set; } = double.NaN;
        public double PeakVolts { get; set; } = double.NaN;
        public string WaveformTitle { get; set; }

        // ----- Histograma da variante selecionada -----
        public VariantStats Stats { get; set; }
        public string VariantName { get; set; }

        public TimeResolutionDisplay WithStats(VariantStats stats, string variantName)
        {
            return new TimeResolutionDisplay
            {
                TimeNs = TimeNs, Volts = Volts, TriggerVolts = TriggerVolts,
                LevelVolts = LevelVolts, BaselineVolts = BaselineVolts,
                BaselineStartNs = BaselineStartNs, BaselineEndNs = BaselineEndNs,
                CrossingNs = CrossingNs, TriggerCrossingNs = TriggerCrossingNs, PeakNs = PeakNs, PeakVolts = PeakVolts,
                WaveformTitle = WaveformTitle,
                Stats = stats, VariantName = variantName
            };
        }
    }

    internal class HistogramSettings
    {
        public int Bins { get; set; }
        public bool AutoRange { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    }

    // Tudo o que é lido e validado na thread da interface antes de iniciar.
    internal class TimeResolutionRunConfig
    {
        public bool Simulated { get; set; }
        public ScopeSettings Scope { get; set; }
        public SimulationSettings Simulation { get; set; }
        public PulseTimingSettings Timing { get; set; }
        public HistogramSettings Histogram { get; set; }
        public int TargetEvents { get; set; }   // eventos válidos; 0 = sem limite
        public bool Verbose { get; set; }
    }

    // Medida de resolução temporal em tempo real, com o osciloscópio (ou simulador).
    // Duas threads: a de aquisição captura blocos e a de análise calcula os Δt.
    // Esta classe não conhece o ScottPlot: entrega TimeResolutionDisplay pelo evento DisplayReady.
    internal class TimeResolutionViewModel : ObservebleObject
    {
        private static readonly string[] VariantNames =
        {
            "Sem correção de XZero",
            "Com +XZero",
            "Com -XZero",
            "Canal do trigger digitalizado"
        };

        // ---------------- Fonte ----------------
        private bool _isSimulated = true;

        public bool IsOscilloscope
        {
            get { return !_isSimulated; }
            set { if (value) SetSimulated(false); }
        }

        public bool IsSimulated
        {
            get { return _isSimulated; }
            set { if (value) SetSimulated(true); }
        }

        private void SetSimulated(bool simulated)
        {
            _isSimulated = simulated;
            OnPropertyChanged("IsOscilloscope");
            OnPropertyChanged("IsSimulated");
        }

        // ---------------- Osciloscópio ----------------
        public string VisaResource { get; set; } = "TCPIP0::152.84.100.65::inst0::INSTR";
        public string ScopeChannelText { get; set; } = "1";
        public string TriggerChannelText { get; set; } = "4";
        public string FrameCountText { get; set; } = "1000";
        public string RecordLengthText { get; set; } = "5000";
        public string SampleRateGsText { get; set; } = "100";
        public string VerticalScaleMvText { get; set; } = "30";
        public string TriggerLevelMvText { get; set; } = "-336";
        public string TriggerPositionText { get; set; } = "11";
        public string TriggerScaleMvText { get; set; } = "200";
        public bool TriggerFalling { get; set; } = true;
        public bool Termination50 { get; set; } = true;
        public bool ReadXZero { get; set; } = true;
        public bool DigitizeTrigger { get; set; } = false;
        public bool VerboseLog { get; set; } = false;

        // ---------------- Análise ----------------
        public bool NegativePulse { get; set; } = false;
        public string ThresholdMvText { get; set; } = "30";
        public int ReferenceModeIndex { get; set; } = 0;   // 0 = fração da amplitude, 1 = limiar fixo
        public string FractionPercentText { get; set; } = "30";
        public string AmplitudePointsText { get; set; } = "5";
        public bool RejectSaturated { get; set; } = false;
        public string BaselineStartText { get; set; } = "-5";
        public string BaselineEndText { get; set; } = "-1";
        public string SearchStartText { get; set; } = "0";
        public string SearchEndText { get; set; } = "40";
        public string TriggerSearchText { get; set; } = "1";
        public bool RequireSingleRun { get; set; } = true;
        public bool LinearInterpolation { get; set; } = true;

        // ---------------- Histograma ----------------
        public string BinsText { get; set; } = "100";
        public bool AutoRange { get; set; } = true;
        public string RangeMinText { get; set; } = "0";
        public string RangeMaxText { get; set; } = "20";
        public string TargetEventsText { get; set; } = "10000";

        private volatile int _selectedVariant = 0;
        public int SelectedVariantIndex
        {
            get { return _selectedVariant; }
            set { _selectedVariant = value < 0 ? 0 : value; OnPropertyChanged(); RepublishStats(); }
        }

        // ---------------- Simulador ----------------
        public string SimDelayNsText { get; set; } = "9.8";
        public string SimJitterPsText { get; set; } = "50";
        public string SimNoiseMvText { get; set; } = "1";
        public string SimAmplitudeDivText { get; set; } = "3";
        public string SimEmptyPercentText { get; set; } = "10";
        public bool SimXZeroPlus { get; set; } = true;

        // ---------------- Estado ----------------
        private bool _isRunning;
        public bool IsRunning
        {
            get { return _isRunning; }
            private set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged("CanEdit"); }
        }

        public bool CanEdit { get { return !_isRunning; } }

        private string _statusText = "Pronto.";
        public string StatusText { get { return _statusText; } private set { _statusText = value; OnPropertyChanged(); } }

        private string _countersText = "";
        public string CountersText { get { return _countersText; } private set { _countersText = value; OnPropertyChanged(); } }

        private string _statsText = "";
        public string StatsText { get { return _statsText; } private set { _statsText = value; OnPropertyChanged(); } }

        public RelayCommand StartCommand { get; private set; }
        public RelayCommand StopCommand { get; private set; }
        public RelayCommand RefreshCommand { get; private set; }

        public event Action<TimeResolutionDisplay> DisplayReady;

        private CancellationTokenSource _cts;
        private TimeResolutionAccumulator _acc = new TimeResolutionAccumulator();
        private volatile HistogramSettings _hist = new HistogramSettings { Bins = 100, AutoRange = true };
        private TimeResolutionDisplay _lastDisplay;
        private double _gridStepNs;       // passo da grade de amostragem (ns); 0 = desconhecido
        private double _gridOriginNs;     // tempo da amostra 0 relativo ao trigger nominal (ns)
        private bool _fixedMode;         // modo de limiar fixo (Δt quantizado na grade se não houver interpolação)
        private DateTime _lastSaturationWarning = DateTime.MinValue;
        private bool _interpolated;       // com interpolação, os Δt deixam de ser quantizados
        private readonly object _displayLock = new object();

        public TimeResolutionViewModel()
        {
            StartCommand = new RelayCommand(async o => await StartAsync(), o => !IsRunning);
            StopCommand = new RelayCommand(o => Stop(), o => IsRunning);
            RefreshCommand = new RelayCommand(o => RefreshHistogram(), o => true);
        }

        private void Stop()
        {
            CancellationTokenSource cts = _cts;
            if (cts != null) cts.Cancel();
            StatusText = "Parando... (aguardando o fim do bloco atual)";
        }

        // Relê as opções do histograma e redesenha com os dados já acumulados.
        private void RefreshHistogram()
        {
            string error;
            HistogramSettings hist = ParseHistogram(out error);
            if (hist == null) { Fail(error); return; }
            _hist = hist;
            if (!IsRunning) RepublishStats();
        }

        // Recalcula estatísticas/histograma de todas as variantes a partir do acumulador.
        private void RepublishStats()
        {
            TimeResolutionDisplay last;
            lock (_displayLock) last = _lastDisplay;
            if (last == null) return;
            PublishStats(_acc, last);
        }

        // ------------------------------------------------------------------
        // Início
        // ------------------------------------------------------------------
        private async Task StartAsync()
        {
            if (IsRunning) return;

            string error;
            TimeResolutionRunConfig cfg = BuildConfig(out error);
            if (cfg == null) { Fail(error); return; }

            _hist = cfg.Histogram;
            _acc = new TimeResolutionAccumulator();
            lock (_displayLock) _lastDisplay = null;
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            CountersText = "";
            StatsText = "";
            StatusText = "Iniciando...";
            IsRunning = true;
            ScanStateManager.SetScanRunning(true);   // bloqueia troca de tela e fechamento do programa
            CommandManager.InvalidateRequerySuggested();

            Exception failure = null;
            try
            {
                await Task.Run(() => Run(cfg, _acc, token));
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
                if (cfg.TargetEvents > 0 && _acc.ValidEvents >= cfg.TargetEvents)
                    StatusText = "Meta de " + cfg.TargetEvents + " eventos válidos atingida.";
                else
                    StatusText = "Parado.";
                Logger.Instance.Log("Time Resolution parado. Eventos válidos: " + _acc.ValidEvents + ".", LogType.Info);
            }
        }

        private void Fail(string message)
        {
            StatusText = "Erro: " + message;
            Logger.Instance.Log(message, LogType.Error);
        }

        // ------------------------------------------------------------------
        // Thread de aquisição (+ thread de análise)
        // ------------------------------------------------------------------
        private void Run(TimeResolutionRunConfig cfg, TimeResolutionAccumulator acc, CancellationToken userToken)
        {
            using (var runCts = CancellationTokenSource.CreateLinkedTokenSource(userToken))
            using (IBlockSource source = cfg.Simulated
                ? (IBlockSource)new SimulatedScope(cfg.Scope, cfg.Simulation)
                : new TektronixScope(cfg.Scope))
            {
                source.Message += text => Logger.Instance.Log(text, LogType.Info);
                if (cfg.Verbose)
                    source.Trace += text => Logger.Instance.Log(text, LogType.Data);

                StatusText = cfg.Simulated ? "Iniciando simulador..." : "Conectando ao osciloscópio...";
                source.Open();
                source.Apply();

                StatusText = "Adquirindo...";
                Logger.Instance.Log("Time Resolution: aquisição iniciada.", LogType.Info);

                var queue = new BlockingCollection<WaveformBlock>(3);
                Exception analysisError = null;

                Task analysis = Task.Run(() =>
                {
                    try
                    {
                        foreach (WaveformBlock b in queue.GetConsumingEnumerable())
                        {
                            ProcessBlock(b, cfg, acc);
                            if (cfg.TargetEvents > 0 && acc.ValidEvents >= cfg.TargetEvents)
                                runCts.Cancel();   // meta atingida: encerra a aquisição
                        }
                    }
                    catch (Exception ex)
                    {
                        analysisError = ex;
                        runCts.Cancel();
                    }
                });

                Exception captureError = null;
                var clock = Stopwatch.StartNew();
                int blocks = 0;
                long frames = 0;
                try
                {
                    while (!runCts.Token.IsCancellationRequested)
                    {
                        WaveformBlock block = source.CaptureBlock(runCts.Token);
                        blocks++;
                        frames += block.FrameCount;
                        queue.Add(block, runCts.Token);

                        double seconds = clock.Elapsed.TotalSeconds;
                        CountersText = string.Format(CultureInfo.InvariantCulture,
                            "Blocos: {0}\nFrames capturados: {1} ({2:0} frames/s)\nEventos válidos: {3} de {4} analisados\nRejeitados: {5}\nAmplitude média: {10:0.0} mV\nÚltimo bloco: captura {6:0} ms | XZero {7:0} ms | CURVe? {8:0} ms | transf. {9:0} ms",
                            blocks, frames, frames / seconds, acc.ValidEvents, acc.FramesAnalyzed,
                            acc.RejectSummary(), block.CaptureMs, block.XZeroMs, block.CurveMs, block.TransferMs, acc.MeanAmplitudeVolts * 1000);
                    }
                }
                catch (OperationCanceledException)
                {
                    // parada do usuário ou meta atingida
                }
                catch (Exception ex)
                {
                    captureError = ex;
                }
                finally
                {
                    queue.CompleteAdding();
                    analysis.Wait();
                }

                // Atualiza os contadores finais depois que a análise terminou.
                CountersText = string.Format(CultureInfo.InvariantCulture,
                    "Blocos: {0}\nFrames capturados: {1}\nEventos válidos: {2} de {3} analisados\nRejeitados: {4}",
                    blocks, frames, acc.ValidEvents, acc.FramesAnalyzed, acc.RejectSummary());

                if (analysisError != null)
                    throw new InvalidOperationException("Erro na análise: " + analysisError.Message, analysisError);
                if (captureError != null)
                    ExceptionDispatchInfo.Capture(captureError).Throw();
            }
        }

        // ------------------------------------------------------------------
        // Análise de um bloco (thread de análise)
        // ------------------------------------------------------------------
        private void ProcessBlock(WaveformBlock block, TimeResolutionRunConfig cfg, TimeResolutionAccumulator acc)
        {
            PulseTimingSettings timing = cfg.Timing;
            int n = block.PointsPerFrame;
            double dtNs = block.XIncrementSeconds * 1e9;
            _gridStepNs = dtNs;
            _gridOriginNs = PulseTimingAnalyzer.IndexToNs(0, block);
            _interpolated = timing.LinearInterpolation;
            _fixedMode = timing.ReferenceMode == PulseReferenceMode.FixedThreshold;
            int saturatedInBlock = 0;
            double[] buffer = new double[n];
            double[] triggerBuffer = block.HasTriggerData ? new double[n] : null;

            int shownFrame = -1;
            PulseTimingResult shownResult = null;
            bool shownIsValid = false;

            for (int f = 0; f < block.FrameCount; f++)
            {
                block.GetFrameVolts(f, buffer);
                PulseTimingResult r = PulseTimingAnalyzer.AnalyzePulse(buffer, block, timing);
                if (r.Saturated)
                {
                    acc.AddSaturated();
                    saturatedInBlock++;
                }

                if (!r.Valid)
                {
                    acc.AddRejected(r.Reason);
                    if (!shownIsValid) { shownFrame = f; shownResult = r; }
                    continue;
                }

                double pulseNs = PulseTimingAnalyzer.IndexToNs(r.CrossingIndex, block);
                double[] dts = { pulseNs, double.NaN, double.NaN, double.NaN };

                if (block.XZeroSeconds != null)
                {
                    double xzNs = block.XZeroSeconds[f] * 1e9;
                    dts[(int)TimingVariant.XZeroPlus] = pulseNs + xzNs;
                    dts[(int)TimingVariant.XZeroMinus] = pulseNs - xzNs;
                }

                if (triggerBuffer != null)
                {
                    block.GetTriggerFrameVolts(f, triggerBuffer);
                    double triggerIndex = PulseTimingAnalyzer.FindTriggerCrossing(triggerBuffer, block, timing);
                    if (double.IsNaN(triggerIndex))
                        acc.AddTriggerMissing();
                    else
                        dts[(int)TimingVariant.DigitizedTrigger] = (r.CrossingIndex - triggerIndex) * dtNs;
                }

                acc.AddEvent(dts);
                acc.AddAmplitude(r.AmplitudeVolts);
                shownFrame = f; shownResult = r; shownIsValid = true;
            }

            if (saturatedInBlock > 0 && (DateTime.Now - _lastSaturationWarning).TotalSeconds > 5)
            {
                _lastSaturationWarning = DateTime.Now;
                Logger.Instance.Log("AVISO: " + saturatedInBlock + " pulso(s) saturaram o ADC neste bloco; a amplitude (e o ponto de fração) fica incorreta. Aumente a escala vertical (mV/div).", LogType.Warning);
            }

            // Waveform mostrada: o último evento válido do bloco (ou o último rejeitado).
            var display = new TimeResolutionDisplay
            {
                TimeNs = block.GetTimeAxisNanoseconds(),
                BaselineStartNs = timing.BaselineStartNs,
                BaselineEndNs = timing.BaselineEndNs
            };
            if (shownFrame >= 0)
            {
                display.Volts = block.GetFrameVolts(shownFrame);
                display.LevelVolts = shownResult.ReferenceVolts;
                display.BaselineVolts = shownResult.BaselineVolts;
                if (shownResult.Valid)
                {
                    display.CrossingNs = PulseTimingAnalyzer.IndexToNs(shownResult.CrossingIndex, block);
                    display.PeakNs = PulseTimingAnalyzer.IndexToNs(shownResult.PeakIndex, block);
                    display.PeakVolts = display.Volts[shownResult.PeakIndex];
                    display.WaveformTitle = "Evento válido (frame " + (shownFrame + 1) + " do último bloco), amplitude " +
                        (shownResult.AmplitudeVolts * 1000).ToString("0.0", CultureInfo.InvariantCulture) + " mV" +
                        (shownResult.Saturated ? " - SATURADO" : "");
                }
                else
                {
                    display.WaveformTitle = "Evento rejeitado (" + shownResult.Reason + ")";
                }

                if (block.HasTriggerData)
                {
                    double[] tv = new double[n];
                    block.GetTriggerFrameVolts(shownFrame, tv);
                    display.TriggerVolts = tv;
                    double tIdx = PulseTimingAnalyzer.FindTriggerCrossing(tv, block, timing);
                    if (!double.IsNaN(tIdx))
                        display.TriggerCrossingNs = PulseTimingAnalyzer.IndexToNs(tIdx, block);
                }
            }

            PublishStats(acc, display);
        }

        // Calcula as estatísticas de todas as variantes, monta o texto e publica.
        private void PublishStats(TimeResolutionAccumulator acc, TimeResolutionDisplay waveformPart)
        {
            HistogramSettings h = _hist;
            var all = new VariantStats[TimeResolutionAccumulator.VariantCount];
            for (int v = 0; v < all.Length; v++)
            {
                // Sem correção e trigger digitalizado (sem interpolação) só assumem valores múltiplos do passo.
                bool quantized = _fixedMode && !_interpolated && (v == (int)TimingVariant.NoCorrection || v == (int)TimingVariant.DigitizedTrigger);
                all[v] = acc.ComputeStats(v, h.AutoRange, h.Min, h.Max, h.Bins, quantized ? _gridStepNs : 0, _gridOriginNs);
            }

            int selected = _selectedVariant;
            if (selected >= all.Length) selected = all.Length - 1;

            StatsText = FormatStats(all, selected);

            TimeResolutionDisplay display = waveformPart.WithStats(all[selected], VariantNames[selected]);
            lock (_displayLock) _lastDisplay = display;

            Action<TimeResolutionDisplay> handler = DisplayReady;
            if (handler != null) handler(display);
        }

        private static string FormatStats(VariantStats[] all, int selected)
        {
            var sb = new StringBuilder();
            sb.AppendLine("  " + "Variante".PadRight(30) + "N".PadLeft(6) + "média(ns)".PadLeft(11) + "std(ps)".PadLeft(9) + "fit σ(ps)".PadLeft(10) + " ± erro");
            for (int v = 0; v < all.Length; v++)
            {
                VariantStats s = all[v];
                sb.Append(v == selected ? "> " : "  ");
                sb.Append(VariantNames[v].PadRight(30));
                if (s.Count == 0)
                {
                    sb.AppendLine("  (sem dados)");
                    continue;
                }
                sb.Append(s.Count.ToString(CultureInfo.InvariantCulture).PadLeft(6));
                sb.Append(s.Mean.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(11));
                sb.Append((s.Std * 1000).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(9));
                if (s.Fit != null && s.Fit.Ok)
                    sb.Append((s.Fit.Sigma * 1000).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(10) +
                              " ± " + (s.Fit.SigmaError * 1000).ToString("0.0", CultureInfo.InvariantCulture));
                else
                    sb.Append("(sem ajuste)".PadLeft(10));
                sb.AppendLine();
            }
            sb.Append("O menor sigma indica a convenção de XZero correta (com pulsos reais).");
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // CSV
        // ------------------------------------------------------------------
        public string BuildCsv()
        {
            return _acc.ValidEvents == 0 ? null : _acc.BuildCsv();
        }

        // ------------------------------------------------------------------
        // Leitura e validação dos campos de texto
        // ------------------------------------------------------------------
        private HistogramSettings ParseHistogram(out string error)
        {
            error = null;
            int bins;
            double min = 0, max = 0;
            if (!TryInt(BinsText, 5, 2000, out bins)) { error = "Número de bins deve ser um inteiro entre 5 e 2000."; return null; }
            if (!AutoRange)
            {
                if (!TryDouble(RangeMinText, out min) || !TryDouble(RangeMaxText, out max) || max <= min)
                {
                    error = "Faixa do histograma inválida (mínimo deve ser menor que o máximo, em ns).";
                    return null;
                }
            }
            return new HistogramSettings { Bins = bins, AutoRange = AutoRange, Min = min, Max = max };
        }

        private TimeResolutionRunConfig BuildConfig(out string error)
        {
            error = null;

            int channel, triggerChannel, frames, recordLength, target;
            double rateGs, scaleMv, levelMv, positionPercent, triggerScaleMv;
            double fractionPercent;
            int amplitudePoints;
            double thresholdMv, baseStart, baseEnd, searchStart, searchEnd, triggerSearch;

            if (!TryInt(ScopeChannelText, 1, 4, out channel)) { error = "Canal medido deve ser um inteiro de 1 a 4."; return null; }
            if (!TryInt(TriggerChannelText, 1, 4, out triggerChannel)) { error = "Canal do trigger deve ser um inteiro de 1 a 4."; return null; }
            if (channel == triggerChannel) { error = "Canal medido e canal do trigger devem ser diferentes."; return null; }
            if (!TryInt(FrameCountText, 1, 1000000, out frames)) { error = "Frames por bloco deve ser um inteiro positivo."; return null; }
            if (!TryInt(RecordLengthText, 2, 100000000, out recordLength)) { error = "Pontos por frame deve ser um inteiro positivo."; return null; }
            if (!TryDouble(SampleRateGsText, out rateGs) || rateGs <= 0) { error = "Taxa de amostragem inválida (GS/s)."; return null; }
            if (!TryDouble(VerticalScaleMvText, out scaleMv) || scaleMv <= 0) { error = "Escala vertical inválida (mV/div)."; return null; }
            if (!TryDouble(TriggerLevelMvText, out levelMv)) { error = "Nível do trigger inválido (mV)."; return null; }
            if (!TryDouble(TriggerPositionText, out positionPercent)) { error = "Posição do trigger inválida (%)."; return null; }
            if (!TryDouble(TriggerScaleMvText, out triggerScaleMv) || triggerScaleMv <= 0) { error = "Escala vertical do canal do trigger inválida (mV/div)."; return null; }
            if (!TryDouble(ThresholdMvText, out thresholdMv) || thresholdMv <= 0) { error = "Limiar inválido (mV, positivo)."; return null; }
            if (!TryDouble(FractionPercentText, out fractionPercent) || fractionPercent <= 0 || fractionPercent >= 100) { error = "Fração da amplitude inválida (entre 0 e 100 %, exclusive)."; return null; }
            if (!TryInt(AmplitudePointsText, 1, 1000, out amplitudePoints)) { error = "Amostras da média da amplitude devem ser um inteiro de 1 a 1000 (1 = pico absoluto; 0 não é aceito)."; return null; }
            if (!TryDouble(BaselineStartText, out baseStart) || !TryDouble(BaselineEndText, out baseEnd)) { error = "Janela da baseline inválida (ns)."; return null; }
            if (!TryDouble(SearchStartText, out searchStart) || !TryDouble(SearchEndText, out searchEnd)) { error = "Janela de busca inválida (ns)."; return null; }
            if (!TryDouble(TriggerSearchText, out triggerSearch)) { error = "Meia-largura da busca do trigger inválida (ns)."; return null; }
            if (!TryInt(TargetEventsText, 0, 100000000, out target)) { error = "Meta de eventos deve ser um inteiro (0 = sem limite)."; return null; }

            HistogramSettings hist = ParseHistogram(out error);
            if (hist == null) return null;

            var scope = new ScopeSettings
            {
                VisaResource = (VisaResource ?? "").Trim(),
                Channel = channel,
                TriggerChannel = triggerChannel,
                FrameCount = frames,
                RecordLength = recordLength,
                SampleRateHz = rateGs * 1e9,
                VerticalScaleVoltsPerDiv = scaleMv / 1000.0,
                TriggerLevelVolts = levelMv / 1000.0,
                TriggerPositionPercent = positionPercent,
                Slope = TriggerFalling ? TriggerSlope.Falling : TriggerSlope.Rising,
                Termination50Ohm = Termination50,
                ReadXZero = ReadXZero,
                KeepTriggerChannelOn = false,
                DigitizeTriggerChannel = DigitizeTrigger,
                TriggerVerticalScaleVoltsPerDiv = triggerScaleMv / 1000.0
            };
            List<string> problems = scope.Validate();
            if (problems.Count > 0) { error = string.Join(" ", problems); return null; }

            var timing = new PulseTimingSettings
            {
                Polarity = NegativePulse ? PulsePolarity.Negative : PulsePolarity.Positive,
                ThresholdVolts = thresholdMv / 1000.0,
                BaselineStartNs = baseStart,
                BaselineEndNs = baseEnd,
                SearchStartNs = searchStart,
                SearchEndNs = searchEnd,
                ReferenceMode = ReferenceModeIndex == 1 ? PulseReferenceMode.FixedThreshold : PulseReferenceMode.ConstantFraction,
                FractionOfAmplitude = fractionPercent / 100.0,
                AmplitudeAveragePoints = amplitudePoints,
                RejectSaturated = RejectSaturated,
                RequireSingleRun = RequireSingleRun,
                LinearInterpolation = LinearInterpolation,
                TriggerFalling = TriggerFalling,
                TriggerLevelVolts = levelMv / 1000.0,
                TriggerSearchHalfWidthNs = triggerSearch
            };
            problems = timing.Validate();
            if (problems.Count > 0) { error = string.Join(" ", problems); return null; }

            var simulation = new SimulationSettings();
            if (_isSimulated)
            {
                double delay, jitter, noise, amp, empty;
                if (!TryDouble(SimDelayNsText, out delay) || !TryDouble(SimJitterPsText, out jitter) ||
                    !TryDouble(SimNoiseMvText, out noise) || !TryDouble(SimAmplitudeDivText, out amp) ||
                    !TryDouble(SimEmptyPercentText, out empty) || jitter < 0 || noise < 0 || amp <= 0 || empty < 0 || empty > 100)
                {
                    error = "Parâmetros do simulador inválidos.";
                    return null;
                }
                simulation.DelayNs = delay;
                simulation.JitterPs = jitter;
                simulation.NoiseMv = noise;
                simulation.AmplitudeDivisions = amp;
                simulation.EmptyFractionPercent = empty;
                simulation.XZeroPlusConvention = SimXZeroPlus;
            }

            return new TimeResolutionRunConfig
            {
                Simulated = _isSimulated,
                Scope = scope,
                Simulation = simulation,
                Timing = timing,
                Histogram = hist,
                TargetEvents = target,
                Verbose = VerboseLog
            };
        }

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
