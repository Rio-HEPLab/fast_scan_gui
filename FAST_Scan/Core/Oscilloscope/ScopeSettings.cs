using System;
using System.Collections.Generic;

namespace FAST_Scan.Core.Oscilloscope
{
    // Borda do sinal em que o trigger dispara.
    public enum TriggerSlope
    {
        Falling,
        Rising
    }

    // Parâmetros da aquisição com o osciloscópio Tektronix (modo FastFrame).
    // É só uma classe de dados: não fala com o instrumento. O TektronixScope
    // é quem traduz estes valores em comandos. Por ter só propriedades públicas,
    // ela pode ser salva em JSON (Newtonsoft) como o AppConfig.
    public class ScopeSettings
    {
        // ----- Conexão -----
        public string VisaResource { get; set; } = "TCPIP0::152.84.100.65::inst0::INSTR";
        public int IoTimeoutMs { get; set; } = 10000;        // consultas comuns
        public int CaptureTimeoutMs { get; set; } = 30000;   // espera pelo fim de um bloco

        // ----- Canal de medida -----
        public int Channel { get; set; } = 1;                // canal digitalizado (1 a 4)
        public double VerticalScaleVoltsPerDiv { get; set; } = 0.030;
        public bool Termination50Ohm { get; set; } = true;

        // ----- Horizontal -----
        public double SampleRateHz { get; set; } = 100e9;
        public int RecordLength { get; set; } = 5000;        // pontos por frame
        public double TriggerPositionPercent { get; set; } = 11;

        // ----- Trigger -----
        public int TriggerChannel { get; set; } = 4;
        public double TriggerLevelVolts { get; set; } = -0.336;
        public TriggerSlope Slope { get; set; } = TriggerSlope.Falling;

        // Medido nos testes: com o canal do trigger ligado o tempo morto
        // multiplica por ~4. Desligado, o trigger continua funcionando.
        public bool KeepTriggerChannelOn { get; set; } = false;

        // Digitalizar também o canal do trigger (para medir o Δt pulso - trigger no mesmo frame).
        // Liga o canal do trigger (mais lento) e ajusta a escala vertical dele.
        public bool DigitizeTriggerChannel { get; set; } = false;
        public double TriggerVerticalScaleVoltsPerDiv { get; set; } = 0.2;

        // ----- FastFrame -----
        public int FrameCount { get; set; } = 1000;          // frames por bloco (configurável)
        public bool ReadXZero { get; set; } = true;          // ler o XZero de cada frame

        // Confere se os valores fazem sentido ANTES de enviar ao instrumento.
        // Devolve a lista de problemas (vazia = tudo certo). Os limites que
        // dependem do instrumento (máximo de frames para o comprimento
        // escolhido, taxas aceitas) são conferidos depois, no TektronixScope.
        public List<string> Validate()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(VisaResource))
                problems.Add("VISA address is empty.");
            if (Channel < 1 || Channel > 4)
                problems.Add("Measurement channel must be between 1 and 4.");
            if (TriggerChannel < 1 || TriggerChannel > 4)
                problems.Add("Trigger channel must be between 1 and 4.");
            if (DigitizeTriggerChannel && Channel == TriggerChannel)
                problems.Add("To digitize the trigger, the measurement channel and the trigger channel must be different.");
            if (DigitizeTriggerChannel && TriggerVerticalScaleVoltsPerDiv <= 0)
                problems.Add("Trigger channel vertical scale must be positive.");
            if (VerticalScaleVoltsPerDiv <= 0)
                problems.Add("Vertical scale must be positive.");
            if (SampleRateHz <= 0)
                problems.Add("Sample rate must be positive.");
            if (RecordLength < 2)
                problems.Add("Frame length must have at least 2 points.");
            if (TriggerPositionPercent < 0 || TriggerPositionPercent > 100)
                problems.Add("Trigger position must be between 0 and 100 %.");
            if (FrameCount < 1)
                problems.Add("Frame count must be at least 1.");
            if (IoTimeoutMs < 1000 || CaptureTimeoutMs < 1000)
                problems.Add("Timeouts must be at least 1000 ms.");

            return problems;
        }

        // Duração de um frame em segundos (comprimento / taxa).
        public double FrameDurationSeconds
        {
            get { return RecordLength / SampleRateHz; }
        }
    }
}
