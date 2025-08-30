using myCar.Models;

namespace myCar.Services
{
    public interface ICostcoService
    {
        Task<PartModel?> GetBatteryPartAsync(HttpClient http, string vin, string zipCode, CancellationToken token);
        Task<List<ProviderModel>> GetClosestLocationsAsync(HttpClient http, string zipCode, double latitude, double longitude, CancellationToken token);
    }

    public class CostcoService : ICostcoService
    {
        private readonly HttpClient http = new();
        private readonly IDatabaseService<PartTypeModel> productTypeService;

        public CostcoService(IDatabaseService<PartTypeModel> productTypeService)
        {
          this.productTypeService = productTypeService;
        }
        
        public async Task<PartModel?> GetBatteryPartAsync(HttpClient http, string vin, string zipCode, CancellationToken token)
        {
            PartTypeModel type = await productTypeService.GetItem("SELECT * FROM [" + Constants.PartTypesTable + "] WHERE Category=\"Battery\";");
            string data = await RequestBatteryAsync(http, vin, zipCode, token);
            return new() { Name = "Battery", SpecValue = data.Between("group_number\": \"","\""), Cost = data.Between("warehouseprice\": \"","\"").ToDecimal(), TypeId = type.Id, Type = type };
            
        }

        private async Task<string> RequestBatteryAsync(HttpClient http, string vin, string zipCode, CancellationToken token)
        {
            var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("vin", vin)]);
            string vehicleOptions = await "https://costco.interstatebatteries.com/api/battery/GetVehicleOptionsByVin".PostJsonAsync(http, content, token);
            string response = await ("https://costco.interstatebatteries.com/results?key=auto&Program=100500&ZipCode=" + zipCode + "&l=" + zipCode + "&Country=United%20States&option=" + vehicleOptions.GetJsonStr("ApplicationId")).GetHtmlAsync(http, token);
            return response.Between("data-model","</tbody>");
        }

        public async Task<List<ProviderModel>> GetClosestLocationsAsync(HttpClient http, string zipCode, double latitude, double longitude, CancellationToken token)
        {
            using CancellationTokenSource cancel = new(TimeSpan.FromSeconds(10));
            var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("latitude", latitude.ToString()), new KeyValuePair<string, string>("longitude", longitude.ToString()), new KeyValuePair<string, string>("datacount", "0"), new KeyValuePair<string, string>("mtext", zipCode), new KeyValuePair<string, string>("IsSingleton", "0")]);
            string result = await "https://tires.costco.com/SearchWarehouseAsync/GetWarehouseDataByLatLong?lang=en-us".PostJsonAsync(http, content, cancel.Token);
            string[] locations = result.Replace("\u003c", "<").Replace("\u003e", ">").Replace("\u0026", "&").Replace("&quot;", "\"").Between("[{\"WarehouseId\":\"", "}]\" />").Split("{\"WarehouseId\":\"");
            List<ProviderModel> results = new();
            for (int i = 0; i < locations.Length; i++)
            {
                string location = locations[i];
                decimal distance = location.Between("Distance\":", ",").ToDecimal();
                if (distance <= 10)
                {
                    if (location.Contains("Tire Service Center"))
                    {
                        results.Add(new ProviderModel
                        {
                            Brand = "Costco",
                            Category = ProviderCategory.Store,
                            Name = location.GetJsonStr("Name"),
                            StoreId = location.Left(4),
                            Address = location.GetJsonStr("Line1") + Environment.NewLine + location.GetJsonStr("City") + ", " + location.GetJsonStr("Territory") + " " + location.GetJsonStr("PostalCode"),
                        });
                    }
                }
                else break;
            }
            return results;
        }
    }
}