using Hollow.Data;

namespace Hollow.Dev
{
    /// <summary>File operations differ between the Editor window and the in-game panel; the host provides them.</summary>
    public interface IBalancePanelHost
    {
        string SaveLabel { get; }
        void Save(BalanceService service);
        void ExportJson(BalanceService service);
        void ImportJson(BalanceService service);
        void ExportCsv(BalanceService service);
        void ImportCsv(BalanceService service);
        string Status { get; }
    }
}
