using System.Collections.Generic;

namespace FAST_Scan.Core.Analysis
{
    public enum PulsePolarity
    {
        Positive,
        Negative
    }

    // Como definir o ponto de referência do pulso na subida.
    public enum PulseReferenceMode
    {
        ConstantFraction,   // fração da amplitude de cada pulso (CFD): elimina o time walk
        FixedThreshold      // nível fixo acima da baseline
    }

    // As quatro formas de calcular o instante do pulso em relação ao trigger.
    // Todas são calculadas ao mesmo tempo: comparar os sigmas é como se descobre
    // qual convenção de XZero está certa (a certa dá o menor sigma).
    public enum TimingVariant
    {
        NoCorrection = 0,     // índice da amostra - índice nominal do trigger
        XZeroPlus = 1,        // ... + XZero do frame
        XZeroMinus = 2,       // ... - XZero do frame
        DigitizedTrigger = 3  // cruzamento do pulso - cruzamento do canal do trigger digitalizado
    }

    // Parâmetros da análise de cada evento (um frame). Tempos em ns relativos ao trigger.
    public class PulseTimingSettings
    {
        public PulsePolarity Polarity { get; set; } = PulsePolarity.Positive;

        public PulseReferenceMode ReferenceMode { get; set; } = PulseReferenceMode.ConstantFraction;

        // Limiar acima (ou abaixo, se negativo) da baseline, em volts (valor positivo).
        // No modo de limiar fixo é o ponto de medida; no modo de fração é só o portão de
        // detecção (separa o pulso do ruído e define o filtro de "exatamente um pulso").
        public double ThresholdVolts { get; set; } = 0.030;

        // Modo de fração: ponto de referência = baseline + fração * amplitude (0 < f < 1).
        public double FractionOfAmplitude { get; set; } = 0.30;

        // Amplitude = média de N amostras em torno do pico (N = 1 é o pico absoluto; N >= 1).
        public int AmplitudeAveragePoints { get; set; } = 5;

        // Pulsos que encostam no fundo de escala do ADC têm amplitude subestimada.
        // Sempre são contados e avisados; com esta opção também são rejeitados.
        public bool RejectSaturated { get; set; } = false;

        // Janela da baseline (média), antes do pulso.
        public double BaselineStartNs { get; set; } = -5.0;
        public double BaselineEndNs { get; set; } = -1.0;

        // Janela onde se procura o pulso.
        public double SearchStartNs { get; set; } = 0.0;
        public double SearchEndNs { get; set; } = 40.0;

        // Exige exatamente UMA região contínua acima do limiar (filtro do notebook,
        // implementado sem o bug de precedência do original).
        public bool RequireSingleRun { get; set; } = true;

        // Interpolação linear entre amostras (aplica-se ao pulso e ao trigger).
        public bool LinearInterpolation { get; set; } = false;

        // Cruzamento do trigger no canal digitalizado.
        public bool TriggerFalling { get; set; } = true;
        public double TriggerLevelVolts { get; set; } = -0.336;
        public double TriggerSearchHalfWidthNs { get; set; } = 1.0;

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (ThresholdVolts <= 0) problems.Add("Limiar deve ser positivo.");
            if (ReferenceMode == PulseReferenceMode.ConstantFraction && (FractionOfAmplitude <= 0 || FractionOfAmplitude >= 1))
                problems.Add("A fração da amplitude deve estar entre 0 e 100 % (exclusive).");
            if (AmplitudeAveragePoints < 1)
                problems.Add("A média da amplitude deve usar pelo menos 1 amostra (1 = pico absoluto).");
            if (BaselineEndNs <= BaselineStartNs) problems.Add("Janela da baseline inválida (fim deve ser maior que início).");
            if (SearchEndNs <= SearchStartNs) problems.Add("Janela de busca do pulso inválida (fim deve ser maior que início).");
            if (TriggerSearchHalfWidthNs <= 0) problems.Add("Meia-largura da busca do trigger deve ser positiva.");
            return problems;
        }
    }
}
