using Robust.Shared.Serialization;

namespace Content.Shared._NF.Bank;

[Serializable, NetSerializable]
public enum LedgerEntryType : byte
{
    // Income entries
    TickingIncome,
    VendorTax,
    CargoTax,
    MailDelivered,
    BlackMarketAtmTax,
    BlackMarketShipyardTax,
    BluespaceReward,
    AntiSmugglingBonus,
    MedicalBountyTax,
    PowerTransmission,
    StationDepositFines,
    StationDepositDonation,
    StationDepositAssetsSold,
    StationDepositOther,
    // Expense entries
    MailPenalty,
    ShuttleRecordFees,
    StationWithdrawalPayroll,
    StationWithdrawalWorkOrder,
    StationWithdrawalSupplies,
    StationWithdrawalBounty,
    StationWithdrawalOther,
    // Utility values
    FirstExpense = MailPenalty,
}
