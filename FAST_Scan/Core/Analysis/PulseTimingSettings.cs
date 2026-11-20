using System.Collections.Generic;

namespace FAST_Scan.Core.Analysis
{
    public enum PulsePolarity
    {
        Positive,
        Negative
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

        // Limiar acima (ou abaixo, se negativo) da baseline, em volts (valor positivo).
        public double ThresholdVolts { get; set; } = 0.030;

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
            if (BaselineEndNs <= BaselineStartNs) problems.Add("Janela da baseline inválida (fim deve ser maior que início).");
            if (SearchEndNs <= SearchStartNs) problems.Add("Janela de busca do pulso inválida (fim deve ser maior que início).");
            if (TriggerSearchHalfWidthNs <= 0) problems.Add("Meia-largura da busca do trigger deve ser positiva.");
            return problems;
        }
    }
}
