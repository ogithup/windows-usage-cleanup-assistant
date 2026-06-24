using System.Globalization;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class MockLlmExplanationService : ILlmExplanationService
{
    public Task<string> GenerateExplanationAsync(ProgramAnalysisDto analysis, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var why = BuildWhySentence(analysis);
        var risk = BuildRiskSentence(analysis);
        var check = BuildCheckSentence(analysis);
        var benefit = BuildBenefitSentence(analysis);

        return Task.FromResult($"{why} {risk} {check} {benefit}".Trim());
    }

    private static string BuildWhySentence(ProgramAnalysisDto analysis)
    {
        return analysis.Recommendation switch
        {
            "Keep" => $"{analysis.ProgramName}, son kullanım ve risk sinyallerine göre şu an tutulması önerilen bir program olarak işaretlendi.",
            "Review" => $"{analysis.ProgramName}, kullanım durumu ile risk sinyalleri karışık olduğu için inceleme gerektiren bir program olarak işaretlendi.",
            "CleanupCandidate" => $"{analysis.ProgramName}, uzun süredir kullanılmıyor görünmesi ve alan tüketimi nedeniyle silinebilir aday olarak işaretlendi.",
            "DoNotRemove" => $"{analysis.ProgramName}, bağımlılık veya sistem riski nedeniyle kaldırılması önerilmeyen bir program olarak işaretlendi.",
            _ => $"{analysis.ProgramName} için yeterli sinyal olmadığı için karar dikkatli yorumlanmalıdır.",
        };
    }

    private static string BuildRiskSentence(ProgramAnalysisDto analysis)
    {
        if (analysis.RiskFlags.Count == 0)
        {
            return "Belirgin bir bağımlılık bayrağı görülmedi, ancak kaldırma öncesinde temel doğrulama yine de gereklidir.";
        }

        var flags = string.Join(", ", analysis.RiskFlags);
        return $"Kaldırılırsa risk oluşturabilecek işaretler: {flags}.";
    }

    private static string BuildCheckSentence(ProgramAnalysisDto analysis)
    {
        return analysis.Category switch
        {
            "Development Tool" => "Kaldırmadan önce aktif projelerinizin bu araca, ilgili SDK bileşenlerine veya build zincirine bağlı olup olmadığını kontrol edin.",
            "System Component" => "Kaldırmadan önce sürücü, donanım yönetimi veya Windows bileşeni bağlantısı olup olmadığını kontrol edin.",
            "Runtime or SDK" => "Kaldırmadan önce başka uygulamaların bu runtime veya SDK paketine ihtiyaç duyup duymadığını kontrol edin.",
            _ => "Kaldırmadan önce son kullanım durumunu, uninstall yolunu ve gerçekten ihtiyaç duyup duymadığınızı kontrol edin.",
        };
    }

    private static string BuildBenefitSentence(ProgramAnalysisDto analysis)
    {
        var sizeLabel = analysis.SizeMB is > 0
            ? $"{analysis.SizeMB.Value.ToString("N0", CultureInfo.InvariantCulture)} MB"
            : "belirsiz miktarda";
        var lastUsed = string.IsNullOrWhiteSpace(analysis.LastUsedLabel) ? "kullanım zamanı bilinmiyor" : analysis.LastUsedLabel;

        return $"Tahmini disk kazanımı {sizeLabel} düzeyinde olabilir; son kullanım bilgisi: {lastUsed}.";
    }
}
