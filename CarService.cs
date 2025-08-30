using myCar.Models;

namespace myCar.Services
{
    public interface ICarService
    {
        Task<bool> LookupAsync(CarModel car, string zipCode, ProviderModel costco);
        Task<bool> UpdateAsync(CarModel car);
    }

    public class CarService : ICarService
    {
        private readonly HttpClient http = new();
        private readonly ICostcoService costco;

        public CarService(ICostcoService costco)
        {
            this.costco = costco;
        }

        public async Task<bool> LookupAsync(CarModel car, string zipCode, ProviderModel costco)
        {
            bool success = false;
            try
            {
                using CancellationTokenSource cancel = new(TimeSpan.FromSeconds(10));
                string hondaData = await RequestJsonAsync(8, 99, "find-honda", "OwnGarage", "getProductByVIN", "{\"divisionId\":\"A\",\"vin\":\"" + car.VIN.ToLower() + "\",\"divisionName\":\"Honda\"}", cancel.Token);
                string modelId = hondaData.GetJsonStr("modelId");
                string specData = await RequestJsonAsync(16, 130, "specifications", "OwnSpecifications", "getAutoSpecificationsByModelId", "{\"modelId\":\"" + modelId + "\",\"divisionId\":\"A\"}", cancel.Token);
                string manualData = await RequestJsonAsync(1, 87, "owners-manuals", "OwnManualsApi", "getManualByVINAuto", "{\"productIdentifier\":\"" + car.VIN + "\",\"divisionId\":\"A\",\"division\":\"Honda\"}", cancel.Token);
                string batteryData = await costco.RequestBatteryInfoAsync(car.VIN, zipCode);
                //
                string bodyStyle = string.Empty;
                string model = hondaData.GetJsonStr("modelGroupName");
                string[] classes = ["Sedan", "Coupe", "Sport Utility"];
                int index = Array.FindIndex(classes, s => model.Contains(s));
                if (index > -1)
                {
                    bodyStyle = classes[index];
                    model = model.Left(bodyStyle).Trim();
                }
                string ownersManual = ResolveOwnersManualUrl(manualData, model, bodyStyle);
                string[] mileages = specData.GetHtmlStr("(City/Highway/Combined)").Split(" / ");
                //
                car.Make = "Honda";
                car.Model = model;
                car.Year = hondaData.GetJsonStr("year");
                car.Trim = hondaData.GetJsonStr("trim").Replace(" w/Leather", "-L").Left(' ');
                car.Name = car.Year + " " + car.Make + " " + car.Model + " " + car.Trim;
                car.BodyStyle = bodyStyle;
                car.ModelId = modelId;
                car.ColorName = hondaData.GetJsonStr("color\":{\"name");
                car.ColorCode = hondaData.GetJsonStr("mfg_color_cd");
                car.Wheels = specData.GetHtmlStr("Wheels");
                car.TireSize = specData.GetHtmlStr("All-Season Tires");
                car.SpareTireSize = specData.GetHtmlStr("Compact Spare Tire");
                car.OwnersManual = ownersManual;
                car.FuelTank = specData.GetHtmlStr("Fuel Tank Capacity");
                car.FuelType = specData.GetHtmlStr("Required Fuel");
                car.MileageCity = mileages[0];
                car.MileageHighway = mileages[1];
                car.MileageCombined = mileages[2];
                //
                success = true;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Request timed out: {ex.Message}");
            }
            return success;
        }

        private async Task<string> RequestBatteryInfoAsync(string vin, string zipCode, CancellationToken token)
        {
            var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("vin", vin)]);
            string vehicleOptions = await "https://costco.interstatebatteries.com/api/battery/GetVehicleOptionsByVin".PostJsonAsync(http, content, token);
            string response = await ("https://costco.interstatebatteries.com/results?key=auto&Program=100500&ZipCode=" + zipCode + "&l=" + zipCode + "&Country=US&option=" + vehicleOptions.GetJsonStr("ApplicationId")).GetHtmlAsync(http, token);
            return response.Between("data-model","</section>");
        }

        private async Task<string> RequestJsonAsync(int r, int id, string pageUri, string controller, string method, string parameters, CancellationToken token)
        {
            using var content = CreateFormContent(pageUri, "{\"actions\":[{\"id\":\"" + id.ToString() + ";a\",\"(descriptor)\":\"aura://ApexActionController/ACTION$execute\",\"callingDescriptor\":\"UNKNOWN\",\"params\":{\"namespace\":\"\",\"classname\":\"" + controller + "Controller\",\"method\":\"" + method + "\",\"params\":" + parameters + ",\"cacheable\":false,\"isContinuation\":false}}]}");
            return await ("https://mygarage.honda.com/s/sfsites/aura?r=" + r.ToString() + "&aura.ApexAction.execute=1").PostJsonAsync(http, content, token);
        }

        private static string ResolveOwnersManualUrl(string manualData, string model, string bodyStyle)
        {
            if (manualData.Contains("isMultiple\": false"))
                return manualData.GetJsonStr("url");
            else
            {
                string bodyType = string.IsNullOrEmpty(bodyStyle) ? model : model + " " + bodyStyle;
                foreach (string manual in manualData.Between("\"manualsList\":[", "]").Split("},{")) {
                    string url = manual.GetJsonStr("url");
                    string title = manual.GetJsonStr("title");
                    if (url.ToLower().EndsWith("pdf") && title.Contains("Owner's Manual") && !title.Contains("Supplement") && title.Contains(bodyType)) return url;
                }
                return string.Empty; // No suitable manual found
            }
        }

        private static FormUrlEncodedContent CreateFormContent(string pageUri, string message) => new([new KeyValuePair<string, string>("message", message), new KeyValuePair<string, string>("aura.context", "{\"mode\":\"PROD\",\"fwuid\":\"eE5UbjZPdVlRT3M0d0xtOXc5MzVOQWg5TGxiTHU3MEQ5RnBMM0VzVXc1cmcxMi42MjkxNDU2LjE2Nzc3MjE2\",\"app\":\"siteforce:communityApp\",\"loaded\":{\"APPLICATION@markup://siteforce:communityApp\":\"1301_LBgf00TjwltnPu835uHgpg\"},\"dn\":[],\"globals\":{},\"uad\":true}"), new KeyValuePair<string, string>("aura.pageURI", $"/s/{pageUri}"), new KeyValuePair<string, string>("aura.token", "null")]);

        public async Task<bool> UpdateAsync(CarModel car)
        {
            // Simulate updating a car asynchronously
            await Task.Delay(1000);
            return true; // Assume the operation was successful
        }
    }
}
