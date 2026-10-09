using DriverChecklist.Api.Models.Enums;
using DriverChecklist.Api.Models.MasterData;

namespace DriverChecklist.Api.Data;

/// <summary>
/// Den fullständiga produktkatalogen enligt README.md avsnitt 2.1: produkt,
/// UN-nummer, fysisk utlastningsplats (station) och vilken checklistemall
/// (typ 1/2/3) som ska användas. Antalet checklistor som genereras beräknas
/// alltid utifrån unika kombinationer av (Template, LoadingStationId).
/// </summary>
public static class ProductCatalog
{
    public static readonly IReadOnlyList<ProductDefinition> Products = new List<ProductDefinition>
    {
        // Typ 1: SAS, PIX, PAX, BDP
        new("SAS", "Saltsyra", "SAS", "UN 1789", "STATION_SAS", ChecklistTemplate.Type1_PixPaxSasBdp),

        new("PIX_111", "PIX 111", "PIX", "UN 2582", "STATION_PIX", ChecklistTemplate.Type1_PixPaxSasBdp),
        new("PIX_113", "PIX 113", "PIX", "UN 2582", "STATION_PIX", ChecklistTemplate.Type1_PixPaxSasBdp),
        new("PIX_118", "PIX 118", "PIX", "UN 2582", "STATION_PIX", ChecklistTemplate.Type1_PixPaxSasBdp),
        new("PIX_311", "PIX 311", "PIX", "UN 2582", "STATION_PIX", ChecklistTemplate.Type1_PixPaxSasBdp),

        new("PAX_15", "PAX 15", "PAX", "UN 1760", "STATION_PAX", ChecklistTemplate.Type1_PixPaxSasBdp),
        new("PAX_60", "PAX 60", "PAX", "UN 1760", "STATION_PAX", ChecklistTemplate.Type1_PixPaxSasBdp),
        new("PAX_100", "PAX 100", "PAX", "UN 1760", "STATION_PAX", ChecklistTemplate.Type1_PixPaxSasBdp),

        new("BDP_865", "BDP 865", "BDP", "UN 1760", "STATION_BDP", ChecklistTemplate.Type1_PixPaxSasBdp),
        new("BDP_870", "BDP 870", "BDP", "UN 1760", "STATION_BDP", ChecklistTemplate.Type1_PixPaxSasBdp),

        // Typ 2: SVS och AKD
        new("SVS_97", "Svavelsyra 94-97%", "SVS", "UN 1830", "STATION_SVS_97", ChecklistTemplate.Type2_SvsAkd),
        new("SVS_98", "Svavelsyra 98%", "SVS", "UN 1830", "STATION_SVS_98_37", ChecklistTemplate.Type2_SvsAkd),
        new("SVS_37", "Svavelsyra 37%", "SVS", "UN 2796", "STATION_SVS_98_37", ChecklistTemplate.Type2_SvsAkd),
        new("AKD_KD364M", "Fennosize KD 364M", "AKD", "UN 1760", "STATION_AKD", ChecklistTemplate.Type2_SvsAkd),

        // Typ 3: ALS och LUT (mallfil saknas ännu - se Services/TemplateResolver.cs)
        new("ALS", "Aluminiumsulfat", "ALS", "UN 3264", "STATION_ALS", ChecklistTemplate.Type3_AlsLut),
        new("LUT", "Natronlut", "LUT", "UN 1824", "STATION_LUT", ChecklistTemplate.Type3_AlsLut),
    };
}
