using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Ivi.Visa;

namespace FAST_Scan.Core.Oscilloscope
{
    // Erro de comunicação ou de configuração do osciloscópio.
    public class ScopeException : Exception
    {
        public ScopeException(string message) : base(message) { }
        public ScopeException(string message, Exception inner) : base(message, inner) { }
    }

    // Valores que o osciloscópio realmente adotou (ele pode ajustar o que foi pedido).
    public class ScopeEffectiveSettings
    {
        public double SampleRateHz { get; set; }
        public int RecordLength { get; set; }
        public double VerticalScaleVoltsPerDiv { get; set; }
        public double TriggerLevelVolts { get; set; }
        public double TriggerPositionPercent { get; set; }
        public int MaxFrames { get; set; }   // máximo de frames para este comprimento
        public int FrameCount { get; set; }
    }

    // Conversa com o osciloscópio Tektronix DPO71254C via VISA.
    // Nesta etapa: abrir/fechar a sessão, guardar/restaurar o estado original,
    // aplicar o ScopeSettings e ler de volta os valores efetivos.
    // A captura de blocos de frames virá numa classe separada.
    //
    // Não é thread-safe: use a mesma instância de uma única thread por vez.
    public class TektronixScope : IBlockSource
    {
        private readonly ScopeSettings settings;
        private IMessageBasedSession session;

        // Comandos que devolvem o osciloscópio ao estado em que o encontramos.
        private readonly List<string> restoreCommands = new List<string>();
        private string originalAcquireState;

        public event Action<string> Message;   // textos informativos para o log/status da interface
        public event Action<string> Trace;     // cada comando enviado (>>) e resposta recebida (<<), para diagnóstico

        public string Idn { get; private set; }
        public bool IsOpen { get { return session != null; } }
        public ScopeEffectiveSettings Effective { get; private set; }
        public List<string> Warnings { get; } = new List<string>();

        public TektronixScope(ScopeSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            this.settings = settings;
        }

        // ------------------------------------------------------------------
        // Abertura e fechamento
        // ------------------------------------------------------------------

        // Abre a sessão, limpa o estado de comunicação e guarda a configuração original.
        public void Open()
        {
            if (IsOpen) return;

            List<string> problems = settings.Validate();
            if (problems.Count > 0)
                throw new ScopeException("Invalid configuration: " + string.Join(" ", problems));

            try
            {
                session = GlobalResourceManager.Open(settings.VisaResource) as IMessageBasedSession;
                if (session == null)
                    throw new ScopeException("The VISA resource is not a message-based instrument.");

                session.TimeoutMilliseconds = settings.IoTimeoutMs;

                // Limpa respostas pendentes de um programa que tenha sido interrompido.
                session.Clear();

                SendCommand("*CLS");
                SendCommand("HEADer OFF");
                SendCommand("VERBose OFF");

                Idn = Query("*IDN?");
                if (!Idn.StartsWith("TEKTRONIX", StringComparison.OrdinalIgnoreCase))
                    Warnings.Add("Instrument not identified as Tektronix: " + Idn);
                Raise("Connected to: " + Idn);

                SaveOriginalState();
                CheckErrors("open the session and save the original configuration");
            }
            catch (ScopeException)
            {
                CloseSession();
                throw;
            }
            catch (Exception ex)
            {
                CloseSession();
                throw new ScopeException("Could not open " + settings.VisaResource + ": " + ex.Message, ex);
            }
        }

        // Restaura a configuração original do osciloscópio e fecha a sessão.
        public void Close()
        {
            if (!IsOpen) return;

            try
            {
                RestoreOriginalState();
            }
            finally
            {
                CloseSession();
            }
        }

        public void Dispose()
        {
            Close();
        }

        // Descarta a sessão atual (possivelmente perdida) e abre outra, já limpa.
        private void Reconnect()
        {
            CloseSession();
            session = GlobalResourceManager.Open(settings.VisaResource) as IMessageBasedSession;
            if (session == null)
                throw new ScopeException("The VISA resource is not a message-based instrument.");
            session.TimeoutMilliseconds = settings.IoTimeoutMs;
            session.Clear();
            SendCommand("*CLS");
            SendCommand("HEADer OFF");
            SendCommand("VERBose OFF");
        }

        private void CloseSession()
        {
            if (session != null)
            {
                try { session.Dispose(); } catch { }
                session = null;
            }
        }

        // ------------------------------------------------------------------
        // Estado original
        // ------------------------------------------------------------------

        // Cada Remember() lê um valor do instrumento e guarda o comando que o devolve.
        private void Remember(string setCommand, string queryCommand)
        {
            restoreCommands.Add(setCommand + " " + Query(queryCommand));
        }

        private void SaveOriginalState()
        {
            restoreCommands.Clear();

            // A ordem importa: é a mesma em que os comandos serão reenviados.
            for (int ch = 1; ch <= 4; ch++)
                Remember("SELect:CH" + ch, "SELect:CH" + ch + "?");

            Remember("HORizontal:MODE", "HORizontal:MODE?");
            Remember("HORizontal:MODE:SAMPLERate", "HORizontal:MODE:SAMPLERate?");
            Remember("HORizontal:MODE:RECOrdlength", "HORizontal:MODE:RECOrdlength?");
            Remember("HORizontal:POSition", "HORizontal:POSition?");

            Remember("TRIGger:A:EDGe:SOUrce", "TRIGger:A:EDGe:SOUrce?");
            Remember("TRIGger:A:EDGe:SLOpe", "TRIGger:A:EDGe:SLOpe?");
            Remember("TRIGger:A:LEVel", "TRIGger:A:LEVel?");
            Remember("TRIGger:A:MODe", "TRIGger:A:MODe?");

            Remember("ACQuire:MODe", "ACQuire:MODe?");
            Remember("CH" + settings.Channel + ":SCAle", "CH" + settings.Channel + ":SCAle?");
            Remember("CH" + settings.Channel + ":TERmination", "CH" + settings.Channel + ":TERmination?");
            if (settings.DigitizeTriggerChannel && settings.TriggerChannel != settings.Channel)
                Remember("CH" + settings.TriggerChannel + ":SCAle", "CH" + settings.TriggerChannel + ":SCAle?");

            Remember("HORizontal:FASTframe:SEQuence", "HORizontal:FASTframe:SEQuence?");
            Remember("HORizontal:FASTframe:COUNt", "HORizontal:FASTframe:COUNt?");
            Remember("HORizontal:FASTframe:STATE", "HORizontal:FASTframe:STATE?");

            Remember("ACQuire:STOPAfter", "ACQuire:STOPAfter?");
            originalAcquireState = Query("ACQuire:STATE?");

            Raise("Original oscilloscope configuration saved.");
        }

        private void RestoreOriginalState()
        {
            var failures = new List<string>();

            // Se a conexão caiu durante a aquisição, tenta reabrir a sessão uma vez
            // para ainda conseguir devolver o osciloscópio à configuração original.
            try
            {
                Query("*OPC?");
            }
            catch (Exception)
            {
                try
                {
                    Reconnect();
                    Raise("Connection lost: session reopened to restore the original configuration.");
                }
                catch (Exception ex)
                {
                    Raise("Warning: connection lost and could not reconnect (" + ex.Message + "). Restore the oscilloscope manually.");
                    return;
                }
            }

            // Para a aquisição antes de mexer em qualquer coisa.
            TrySend("ACQuire:STATE OFF", failures);
            TrySend("HORizontal:FASTframe:STATE OFF", failures);

            foreach (string command in restoreCommands)
                TrySend(command, failures);

            // A aquisição só volta ao que era por último.
            if (originalAcquireState != null)
                TrySend("ACQuire:STATE " + originalAcquireState, failures);

            try { SendCommand("*CLS"); } catch { }

            if (failures.Count > 0)
                Raise("Warning: failed to restore: " + string.Join("; ", failures));
            else
                Raise("Original configuration restored.");
        }

        private void TrySend(string command, List<string> failures)
        {
            try { SendCommand(command); }
            catch (Exception ex) { failures.Add(command + " (" + ex.Message + ")"); }
        }

        // ------------------------------------------------------------------
        // Aplicar configuração
        // ------------------------------------------------------------------

        // Envia o ScopeSettings ao osciloscópio e lê de volta o que ele adotou.
        public void Apply()
        {
            RequireOpen();

            List<string> problems = settings.Validate();
            if (problems.Count > 0)
                throw new ScopeException("Invalid configuration: " + string.Join(" ", problems));

            Warnings.Clear();
            string ch = "CH" + settings.Channel;
            string trigCh = "CH" + settings.TriggerChannel;

            // 1) Para a aquisição e sai do FastFrame para mexer no horizontal.
            SendCommand("ACQuire:STATE OFF");
            SendCommand("HORizontal:FASTframe:STATE OFF");

            // 2) Canais: só o de medida (e o do trigger, se pedido) ficam ligados.
            //    Canais extras reduzem a taxa de amostragem.
            for (int i = 1; i <= 4; i++)
            {
                bool on = (i == settings.Channel) || (i == settings.TriggerChannel && (settings.KeepTriggerChannelOn || settings.DigitizeTriggerChannel));
                SendCommand("SELect:CH" + i + (on ? " ON" : " OFF"));
            }

            CheckErrors("stop the acquisition and switch channels on/off");

            // 3) Vertical.
            SendCommand(ch + ":SCAle " + Num(settings.VerticalScaleVoltsPerDiv));
            SendCommand(ch + ":TERmination " + (settings.Termination50Ohm ? "50" : "1.0E+6"));
            if (settings.DigitizeTriggerChannel)
                SendCommand(trigCh + ":SCAle " + Num(settings.TriggerVerticalScaleVoltsPerDiv));
            SendCommand("ACQuire:MODe SAMple");

            CheckErrors("configure the vertical settings");

            // 4) Horizontal manual.
            SendCommand("HORizontal:MODE MANual");
            SendCommand("HORizontal:MODE:SAMPLERate " + Num(settings.SampleRateHz));
            SendCommand("HORizontal:MODE:RECOrdlength " + settings.RecordLength.ToString(CultureInfo.InvariantCulture));
            SendCommand("HORizontal:POSition " + Num(settings.TriggerPositionPercent));

            CheckErrors("configure the horizontal settings");

            // 5) Trigger.
            SendCommand("TRIGger:A:EDGe:SOUrce " + trigCh);
            SendCommand("TRIGger:A:EDGe:SLOpe " + (settings.Slope == TriggerSlope.Falling ? "FALL" : "RISe"));
            SendCommand("TRIGger:A:LEVel " + Num(settings.TriggerLevelVolts));
            SendCommand("TRIGger:A:MODe NORMal");

            CheckErrors("configure the trigger");

            // 6) FastFrame: cada captura é uma sequência única de FrameCount frames.
            SendCommand("ACQuire:STOPAfter SEQuence");
            SendCommand("HORizontal:FASTframe:STATE ON");

            var effective = new ScopeEffectiveSettings();
            effective.MaxFrames = QueryInt("HORizontal:FASTframe:MAXFRames?");
            if (settings.FrameCount > effective.MaxFrames)
                throw new ScopeException("Frame count (" + settings.FrameCount + ") exceeds the oscilloscope maximum for this record length (" + effective.MaxFrames + ").");

            SendCommand("HORizontal:FASTframe:COUNt " + settings.FrameCount.ToString(CultureInfo.InvariantCulture));
            SendCommand("HORizontal:FASTframe:SEQuence FIRst");

            CheckErrors("configure FastFrame");

            // 7) Lê de volta o que valeu.
            effective.SampleRateHz = QueryDouble("HORizontal:MODE:SAMPLERate?");
            effective.RecordLength = QueryInt("HORizontal:MODE:RECOrdlength?");
            effective.VerticalScaleVoltsPerDiv = QueryDouble(ch + ":SCAle?");
            effective.TriggerLevelVolts = QueryDouble("TRIGger:A:LEVel?");
            effective.TriggerPositionPercent = QueryDouble("HORizontal:POSition?");
            effective.FrameCount = QueryInt("HORizontal:FASTframe:COUNt?");
            Effective = effective;

            // 8) Transferência: um único bloco com todos os frames, 1 byte por ponto.
            SendCommand("DATa:SOUrce " + ch);
            SendCommand("DATa:ENCdg RIBinary");
            SendCommand("WFMOutpre:BYT_Nr 1");
            SendCommand("DATa:STARt 1");
            SendCommand("DATa:STOP " + effective.RecordLength.ToString(CultureInfo.InvariantCulture));
            SendCommand("DATa:FRAMESTARt 1");
            SendCommand("DATa:FRAMESTOP " + effective.FrameCount.ToString(CultureInfo.InvariantCulture));

            CheckErrors("apply configuration");

            // 9) Avisos quando o osciloscópio ajustou algo importante.
            if (Differs(effective.SampleRateHz, settings.SampleRateHz))
                Warnings.Add("Sample rate adjusted by the oscilloscope: requested " + settings.SampleRateHz + " Hz, actual " + effective.SampleRateHz + " Hz.");
            if (effective.RecordLength != settings.RecordLength)
                Warnings.Add("Record length adjusted: requested " + settings.RecordLength + ", actual " + effective.RecordLength + ".");
            if (Differs(effective.VerticalScaleVoltsPerDiv, settings.VerticalScaleVoltsPerDiv))
                Warnings.Add("Vertical scale adjusted: requested " + settings.VerticalScaleVoltsPerDiv + " V/div, actual " + effective.VerticalScaleVoltsPerDiv + " V/div.");

            foreach (string w in Warnings) Raise("WARNING: " + w);
            Raise("Configuration applied: " + effective.FrameCount + " frames of " + effective.RecordLength + " points at " + (effective.SampleRateHz / 1e9).ToString("0.###", CultureInfo.InvariantCulture) + " GS/s.");
        }

        // ------------------------------------------------------------------
        // Comunicação de baixo nível (usada também pela classe de captura)
        // ------------------------------------------------------------------

        internal void SendCommand(string command)
        {
            RequireOpen();
            try { session.RawIO.Write(command + "\n"); RaiseTrace(">> " + command); }
            catch (Exception ex) { throw new ScopeException("Failed to send \"" + command + "\": " + ex.Message, ex); }
        }

        internal string Query(string command)
        {
            SendCommand(command);
            try
            {
                string response = session.RawIO.ReadString().Trim();
                RaiseTrace("<< " + response);
                return response;
            }
            catch (Exception ex) { throw new ScopeException("Failed to read response to \"" + command + "\": " + ex.Message, ex); }
        }

        internal double QueryDouble(string command)
        {
            string text = Query(command);
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new ScopeException("Invalid numeric response to \"" + command + "\": \"" + text + "\"");
            return value;
        }

        internal int QueryInt(string command)
        {
            return (int)Math.Round(QueryDouble(command));
        }

        // Erros de comando não geram exceção no VISA: ficam no registrador *ESR?.
        // Se houver algum, lê os textos com ALLEv? e lança ScopeException.
        internal void CheckErrors(string context)
        {
            int esr = QueryInt("*ESR?");
            if (esr == 0) return;

            string details = Query("ALLEv?");
            throw new ScopeException("The oscilloscope reported an error when trying to " + context + " (ESR=" + esr + "): " + details);
        }

        // ------------------------------------------------------------------
        // Captura de um bloco FastFrame
        // ------------------------------------------------------------------

        // Dispara UMA captura de FrameCount frames, espera o fim e transfere tudo.
        // Bloqueia até o bloco terminar (FrameCount / taxa de triggers, mais o tempo
        // morto). O cancelamento só é verificado ANTES de começar um bloco.
        // Chame Apply() antes.
        public WaveformBlock CaptureBlock(CancellationToken token)
        {
            RequireOpen();
            if (Effective == null)
                throw new ScopeException("Call Apply() before capturing.");
            token.ThrowIfCancellationRequested();

            var block = new WaveformBlock();
            var watch = Stopwatch.StartNew();

            // 1) Arma a sequência única e espera o fim com *OPC? (sem polling).
            //    O timeout é ampliado só durante a espera.
            SendCommand("ACQuire:STATE ON");
            session.TimeoutMilliseconds = settings.CaptureTimeoutMs;
            try
            {
                Query("*OPC?");
            }
            catch (ScopeException ex)
            {
                if (ex.InnerException is IOTimeoutException)
                {
                    // Sem trigger a captura nunca termina: aborta e limpa a comunicação.
                    try { session.Clear(); SendCommand("ACQuire:STATE OFF"); } catch { }
                    throw new ScopeException("Timed out waiting for the block. Is there a trigger signal on CH" + settings.TriggerChannel + " and is the level correct?", ex);
                }
                throw;
            }
            finally
            {
                session.TimeoutMilliseconds = settings.IoTimeoutMs;
            }
            block.CaptureMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();

            // 2) Preâmbulo: só vale depois de uma aquisição válida.
            block.PointsPerFrame = QueryInt("WFMOutpre:NR_Pt?");
            block.FrameCount = QueryInt("WFMOutpre:NR_FR?");
            block.YMultiplier = QueryDouble("WFMOutpre:YMUlt?");
            block.YOffset = QueryDouble("WFMOutpre:YOFf?");
            block.YZero = QueryDouble("WFMOutpre:YZEro?");
            block.XIncrementSeconds = QueryDouble("WFMOutpre:XINcr?");
            block.TriggerIndex = Effective.TriggerPositionPercent / 100.0 * block.PointsPerFrame;

            // 3) XZero de cada frame (opcional).
            if (settings.ReadXZero)
            {
                var xzeroWatch = Stopwatch.StartNew();
                block.XZeroSeconds = ReadXZeros(block.FrameCount);
                block.XZeroMs = xzeroWatch.Elapsed.TotalMilliseconds;
            }

            // 4) Todos os frames num único CURVe?.
            var curveWatch = Stopwatch.StartNew();
            SendCommand("CURVe?");
            byte[] raw = ReadBinaryBlock();
            block.CurveMs = curveWatch.Elapsed.TotalMilliseconds;
            long expected = (long)block.FrameCount * block.PointsPerFrame;
            if (raw.Length != expected)
                throw new ScopeException("Block with unexpected size: received " + raw.Length + " bytes, expected " + expected + ".");
            block.RawData = raw;

            // 5) Digitized trigger channel (opcional): troca a fonte, lê preâmbulo e dados, volta.
            if (settings.DigitizeTriggerChannel)
            {
                SendCommand("DATa:SOUrce CH" + settings.TriggerChannel);
                block.TriggerYMultiplier = QueryDouble("WFMOutpre:YMUlt?");
                block.TriggerYOffset = QueryDouble("WFMOutpre:YOFf?");
                block.TriggerYZero = QueryDouble("WFMOutpre:YZEro?");
                SendCommand("CURVe?");
                byte[] triggerRaw = ReadBinaryBlock();
                if (triggerRaw.Length != expected)
                    throw new ScopeException("Trigger block with unexpected size: received " + triggerRaw.Length + " bytes, expected " + expected + ".");
                block.TriggerRawData = triggerRaw;
                SendCommand("DATa:SOUrce CH" + settings.Channel);
                block.CurveMs = curveWatch.Elapsed.TotalMilliseconds;
            }

            CheckErrors("capture block");
            block.TransferMs = watch.Elapsed.TotalMilliseconds;
            return block;
        }

        // O ReadString do VISA trunca respostas de ~1 KB: lê o XZero em grupos de 20 frames.
        // Formato da resposta: "1: 2.5265E-009","2: 3.1E-009",...
        private double[] ReadXZeros(int frameCount)
        {
            const int chunkSize = 20;
            double[] result = new double[frameCount];

            for (int first = 1; first <= frameCount; first += chunkSize)
            {
                int last = Math.Min(first + chunkSize - 1, frameCount);
                string text = Query("HORizontal:FASTframe:XZEro:ALL:CH" + settings.Channel + "? " + first + "," + last);

                MatchCollection matches = Regex.Matches(text, "\"\\s*\\d+\\s*:\\s*([^\"]+?)\\s*\"");
                if (matches.Count != last - first + 1)
                    throw new ScopeException("XZero: expected " + (last - first + 1) + " values, received " + matches.Count + ".");

                for (int k = 0; k < matches.Count; k++)
                    result[first - 1 + k] = double.Parse(matches[k].Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Dados binários
        // ------------------------------------------------------------------

        private byte[] ReadBytes(int count)
        {
            byte[] result = new byte[count];
            int received = 0;
            while (received < count)
            {
                byte[] chunk = session.RawIO.Read(count - received);
                Buffer.BlockCopy(chunk, 0, result, received, chunk.Length);
                received += chunk.Length;
            }
            return result;
        }

        // Formato IEEE 488.2: #<n><tamanho com n dígitos><dados><LF>
        private byte[] ReadBinaryBlock()
        {
            try
            {
                byte[] header = ReadBytes(2);
                if (header[0] != (byte)'#')
                    throw new ScopeException("The response does not start with '#': not a binary block.");
                int digitCount = header[1] - (byte)'0';
                int length = int.Parse(Encoding.ASCII.GetString(ReadBytes(digitCount)), CultureInfo.InvariantCulture);
                byte[] data = ReadBytes(length);
                ReadBytes(1);   // terminador de linha
                return data;
            }
            catch (ScopeException) { throw; }
            catch (Exception ex) { throw new ScopeException("Failed to read the binary block: " + ex.Message, ex); }
        }

        // ------------------------------------------------------------------
        // Auxiliares
        // ------------------------------------------------------------------

        private static string Num(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool Differs(double a, double b)
        {
            if (b == 0) return a != 0;
            return Math.Abs(a - b) / Math.Abs(b) > 0.01;   // tolerância de 1 %
        }

        private void RequireOpen()
        {
            if (!IsOpen) throw new ScopeException("The oscilloscope session is not open.");
        }

        private void RaiseTrace(string text)
        {
            Action<string> handler = Trace;
            if (handler != null) handler(text);
        }

        private void Raise(string text)
        {
            Action<string> handler = Message;
            if (handler != null) handler(text);
        }
    }
}
