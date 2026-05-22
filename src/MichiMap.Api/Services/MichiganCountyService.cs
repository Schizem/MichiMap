namespace MichiMap.Api.Services;

public record CountyInfo(string Fips, decimal Lat, decimal Lng);

// Resolves a Michigan county name to its FIPS code and approximate centroid.
// Centroid coordinates are used for morel submissions so exact foraging spots
// are never stored. Only the county-level location is recorded.
public class MichiganCountyService
{
    // All 83 Michigan counties: name -> (FIPS, centroid lat, centroid lng)
    private static readonly Dictionary<string, CountyInfo> Counties =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alcona"]        = new("26001",  44.6706m, -83.3738m),
        ["Alger"]         = new("26003",  46.4406m, -86.4914m),
        ["Allegan"]       = new("26005",  42.5930m, -85.8914m),
        ["Alpena"]        = new("26007",  45.0706m, -83.6570m),
        ["Antrim"]        = new("26009",  45.0206m, -85.1847m),
        ["Arenac"]        = new("26011",  44.0383m, -83.7433m),
        ["Baraga"]        = new("26013",  46.8119m, -88.4933m),
        ["Barry"]         = new("26015",  42.5994m, -85.3086m),
        ["Bay"]           = new("26017",  43.6667m, -83.9667m),
        ["Benzie"]        = new("26019",  44.7011m, -86.1175m),
        ["Berrien"]       = new("26021",  41.9253m, -86.4278m),
        ["Branch"]        = new("26023",  41.9131m, -85.0614m),
        ["Calhoun"]       = new("26025",  42.2444m, -84.9994m),
        ["Cass"]          = new("26027",  41.9133m, -85.9892m),
        ["Charlevoix"]    = new("26029",  45.2703m, -85.2703m),
        ["Cheboygan"]     = new("26031",  45.4997m, -84.4914m),
        ["Chippewa"]      = new("26033",  46.3256m, -84.3500m),
        ["Clare"]         = new("26035",  43.9939m, -84.8453m),
        ["Clinton"]       = new("26037",  42.9467m, -84.5997m),
        ["Crawford"]      = new("26039",  44.6836m, -84.6089m),
        ["Delta"]         = new("26041",  45.8911m, -87.0003m),
        ["Dickinson"]     = new("26043",  45.9617m, -87.8628m),
        ["Eaton"]         = new("26045",  42.5744m, -84.6453m),
        ["Emmet"]         = new("26047",  45.5997m, -84.9625m),
        ["Genesee"]       = new("26049",  43.0197m, -83.6836m),
        ["Gladwin"]       = new("26051",  43.9814m, -84.3789m),
        ["Gogebic"]       = new("26053",  46.5594m, -89.7844m),
        ["Grand Traverse"] = new("26055", 44.7631m, -85.5547m),
        ["Gratiot"]       = new("26057",  43.2839m, -84.6197m),
        ["Hillsdale"]     = new("26059",  41.8922m, -84.6086m),
        ["Houghton"]      = new("26061",  47.0394m, -88.5494m),
        ["Huron"]         = new("26063",  43.9992m, -82.9739m),
        ["Ingham"]        = new("26065",  42.5994m, -84.3733m),
        ["Ionia"]         = new("26067",  42.9467m, -85.0697m),
        ["Iosco"]         = new("26069",  44.3281m, -83.5189m),
        ["Iron"]          = new("26071",  46.2947m, -88.8597m),
        ["Isabella"]      = new("26073",  43.6383m, -84.8414m),
        ["Jackson"]       = new("26075",  42.2594m, -84.4036m),
        ["Kalamazoo"]     = new("26077",  42.2522m, -85.5536m),
        ["Kalkaska"]      = new("26079",  44.7281m, -85.1714m),
        ["Kent"]          = new("26081",  43.0317m, -85.5478m),
        ["Keweenaw"]      = new("26083",  47.4589m, -87.9006m),
        ["Lake"]          = new("26085",  43.9714m, -85.8197m),
        ["Lapeer"]        = new("26087",  43.0914m, -83.2286m),
        ["Leelanau"]      = new("26089",  45.0033m, -85.8375m),
        ["Lenawee"]       = new("26091",  41.8939m, -84.0472m),
        ["Livingston"]    = new("26093",  42.5728m, -83.9125m),
        ["Luce"]          = new("26095",  46.6003m, -85.4092m),
        ["Mackinac"]      = new("26097",  45.9125m, -84.5828m),
        ["Macomb"]        = new("26099",  42.6672m, -82.9503m),
        ["Manistee"]      = new("26101",  44.2419m, -86.3244m),
        ["Marquette"]     = new("26103",  46.5542m, -87.5589m),
        ["Mason"]         = new("26105",  43.8867m, -86.0467m),
        ["Mecosta"]       = new("26107",  43.6381m, -85.4239m),
        ["Menominee"]     = new("26109",  45.5528m, -87.5517m),
        ["Midland"]       = new("26111",  43.6736m, -84.3800m),
        ["Missaukee"]     = new("26113",  44.3381m, -85.1114m),
        ["Monroe"]        = new("26115",  41.9178m, -83.5403m),
        ["Montcalm"]      = new("26117",  43.2983m, -85.1597m),
        ["Montmorency"]   = new("26119",  44.9503m, -84.1228m),
        ["Muskegon"]      = new("26121",  43.2369m, -86.0644m),
        ["Newaygo"]       = new("26123",  43.5217m, -85.8003m),
        ["Oakland"]       = new("26125",  42.6606m, -83.3806m),
        ["Oceana"]        = new("26127",  43.6808m, -86.2578m),
        ["Ogemaw"]        = new("26129",  44.3314m, -84.1236m),
        ["Ontonagon"]     = new("26131",  46.7028m, -89.3578m),
        ["Osceola"]       = new("26133",  43.9956m, -85.3297m),
        ["Oscoda"]        = new("26135",  44.6703m, -84.1236m),
        ["Otsego"]        = new("26137",  45.0281m, -84.6061m),
        ["Ottawa"]        = new("26139",  42.9419m, -86.0381m),
        ["Presque Isle"]  = new("26141",  45.4256m, -83.9511m),
        ["Roscommon"]     = new("26143",  44.3614m, -84.6097m),
        ["Saginaw"]       = new("26145",  43.4197m, -83.9508m),
        ["St. Clair"]     = new("26147",  42.9319m, -82.6578m),
        ["St. Joseph"]    = new("26149",  41.9272m, -85.5367m),
        ["Sanilac"]       = new("26151",  43.4278m, -82.6722m),
        ["Schoolcraft"]   = new("26153",  46.1783m, -86.2097m),
        ["Shiawassee"]    = new("26155",  42.9417m, -84.1517m),
        ["Tuscola"]       = new("26157",  43.5003m, -83.2992m),
        ["Van Buren"]     = new("26159",  42.2911m, -86.0292m),
        ["Washtenaw"]     = new("26161",  42.2733m, -83.8511m),
        ["Wayne"]         = new("26163",  42.3314m, -83.2686m),
        ["Wexford"]       = new("26165",  44.3383m, -85.5783m),
    };

    public CountyInfo? Lookup(string countyName) =>
        Counties.TryGetValue(countyName, out var info) ? info : null;

    public IEnumerable<string> AllCountyNames => Counties.Keys.Order();

    public bool IsValidCounty(string countyName) => Counties.ContainsKey(countyName);
}
