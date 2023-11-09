using Newtonsoft.Json;

namespace LineBot_Cosmos
{
    public class AirBoxes
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        public AirBoxSite airBoxSite;
    }

    public class AirBoxSite
    {
        public string WaterTower_Left { get; set; }
        public string WaterTower_Center { get; set; }
        public string WaterTower_Right { get; set; }
        public string TCDorm_Left { get; set; }
        public string TCDorm_Center { get; set; }
        public string TCDorm_Right { get; set; }
        public string XinKeRoad { get; set; }
        public string Golf_Left { get; set; }
        public string Golf_Right { get; set; }
        public string Test { get; set; }
    }
}
