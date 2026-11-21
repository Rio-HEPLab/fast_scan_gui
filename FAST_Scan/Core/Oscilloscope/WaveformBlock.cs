using System;

namespace FAST_Scan.Core.Oscilloscope
{
    // Resultado de uma captura FastFrame: todos os frames de um bloco,
    // ainda em bytes brutos, mais o necessário para convertê-los em volts e tempo.
    // Guardar os bytes (e não doubles) economiza memória e tempo de conversão:
    // só convertemos os frames que realmente vão ser desenhados ou analisados.
    public class WaveformBlock
    {
        // Frames concatenados, 1 byte por ponto (RIBinary = inteiro COM sinal).
        public byte[] RawData { get; set; }

        public int FrameCount { get; set; }
        public int PointsPerFrame { get; set; }

        // Conversão do manual: volts = YZero + YMultiplier * (valor - YOffset)
        public double YMultiplier { get; set; }
        public double YOffset { get; set; }
        public double YZero { get; set; }

        // Canal do trigger digitalizado (opcional): mesmo formato, preâmbulo próprio.
        public byte[] TriggerRawData { get; set; }
        public double TriggerYMultiplier { get; set; }
        public double TriggerYOffset { get; set; }
        public double TriggerYZero { get; set; }
        public bool HasTriggerData { get { return TriggerRawData != null || TriggerSamples != null; } }

        // Modo float (digitizer): amostras já em unidades de ADC, sem bytes. Se Samples != null,
        // ele é usado no lugar de RawData; a conversão em volts é a mesma
        // (YZero + YMultiplier * (valor - YOffset)).
        public float[] Samples { get; set; }
        public float[] TriggerSamples { get; set; }

        // Códigos considerados saturados (fundo de escala). Padrão: ADC de 8 bits do osciloscópio.
        public double SaturationHighCode { get; set; } = 126;
        public double SaturationLowCode { get; set; } = -127;

        // Intervalo entre amostras (s) e índice aproximado da amostra do trigger.
        public double XIncrementSeconds { get; set; }
        public double TriggerIndex { get; set; }

        // XZero de cada frame (s); null se ReadXZero estava desligado.
        public double[] XZeroSeconds { get; set; }

        // Tempos medidos desta captura.
        public double CaptureMs { get; set; }
        public double TransferMs { get; set; }   // preâmbulo + XZero + CURVe?
        public double XZeroMs { get; set; }      // só a leitura dos XZero
        public double CurveMs { get; set; }      // só o CURVe? (transferência dos dados)

        // Converte um frame (0 a FrameCount-1) para volts.
        public double[] GetFrameVolts(int frame)
        {
            double[] volts = new double[PointsPerFrame];
            GetFrameVolts(frame, volts);
            return volts;
        }

        // Versão sem alocação: escreve em 'destination' (tamanho >= PointsPerFrame).
        public void GetFrameVolts(int frame, double[] destination)
        {
            if (Samples != null)
                ConvertFloat(Samples, frame, YMultiplier, YOffset, YZero, destination);
            else
                Convert(RawData, frame, YMultiplier, YOffset, YZero, destination);
        }

        // Frame do canal do trigger digitalizado.
        public void GetTriggerFrameVolts(int frame, double[] destination)
        {
            if (!HasTriggerData)
                throw new InvalidOperationException("O bloco não tem dados do canal do trigger.");
            if (TriggerSamples != null)
                ConvertFloat(TriggerSamples, frame, TriggerYMultiplier, TriggerYOffset, TriggerYZero, destination);
            else
                Convert(TriggerRawData, frame, TriggerYMultiplier, TriggerYOffset, TriggerYZero, destination);
        }

        private void ConvertFloat(float[] raw, int frame, double yMult, double yOff, double yZero, double[] destination)
        {
            if (frame < 0 || frame >= FrameCount)
                throw new ArgumentOutOfRangeException("frame");

            int start = frame * PointsPerFrame;
            for (int i = 0; i < PointsPerFrame; i++)
                destination[i] = yZero + yMult * (raw[start + i] - yOff);
        }

        private void Convert(byte[] raw, int frame, double yMult, double yOff, double yZero, double[] destination)
        {
            if (frame < 0 || frame >= FrameCount)
                throw new ArgumentOutOfRangeException("frame");

            int start = frame * PointsPerFrame;
            for (int i = 0; i < PointsPerFrame; i++)
            {
                sbyte value = unchecked((sbyte)raw[start + i]);
                destination[i] = yZero + yMult * (value - yOff);
            }
        }

        // Tensões dos códigos 126 e -127 do ADC de 8 bits (quase fundo de escala):
        // amostras além delas indicam sinal saturado.
        public double SaturationHighVolts { get { return YZero + YMultiplier * (SaturationHighCode - YOffset); } }
        public double SaturationLowVolts { get { return YZero + YMultiplier * (SaturationLowCode - YOffset); } }

        // Eixo de tempo nominal em ns, com 0 na amostra do trigger.
        // Não aplica o XZero (ordem de ps): serve para exibir, não para medir jitter.
        public double[] GetTimeAxisNanoseconds()
        {
            double[] time = new double[PointsPerFrame];
            for (int i = 0; i < PointsPerFrame; i++)
                time[i] = (i - TriggerIndex) * XIncrementSeconds * 1e9;
            return time;
        }
    }
}
