using System;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using System.Reflection;
using System.Linq;


namespace LineBot_Cosmos
{
    class Program
    {
        private static readonly string EndpointUri = ConfigurationManager.AppSettings["EndPointUri"];

        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = ConfigurationManager.AppSettings["PrimaryKey"];

        // The Cosmos client instance
        private CosmosClient cosmosClient;

        // The database we will create
        private Database database;

        // The container we will create.
        private Container container;

        // The name of the database and container we will create
        private string databaseId = "ToDoList";
        private string containerId = "Items";

        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Beginning operations...\n");
                Program p = new Program();
                await p.StartExecute();
            }
            catch (CosmosException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}", de.StatusCode, de);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");

                Console.ReadKey();
            }
        }

        public async Task StartExecute()
        {
            this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = "CosmosDBDotnetQuickstart" });
            await this.CreateDatabaseAsync();
            await this.CreateContainerAsync();
            await this.Init();
            var _ = new WebClient().DownloadString(ConfigurationManager.AppSettings["LineEndPoint"]);
        }

        // <CreateDatabaseAsync>
        /// <summary>
        /// Create the database if it does not exist
        /// </summary>
        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            Console.WriteLine("Created Database: {0}\n", this.database.Id);
        }
        // </CreateDatabaseAsync>

        // <CreateContainerAsync>
        /// <summary>
        /// Create the container if it does not exist. 
        /// Specifiy "/partitionKey" as the partition key path since we're storing family information, to ensure good distribution of requests and storage.
        /// </summary>
        /// <returns></returns>
        private async Task CreateContainerAsync()
        {
            // Create a new container
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/partitionKey");
            Console.WriteLine("Created Container: {0}\n", this.container.Id);
        }
        // </CreateContainerAsync>

        public async Task Init()
        {
            ItemResponse<AirBoxes> airBoxesResponse = await this.container.ReadItemAsync<AirBoxes>("AirBoxes", new PartitionKey("AirBoxes"));
            var itemBody = airBoxesResponse.Resource;
            AirBoxSite airBoxSites = itemBody.airBoxSite;
            var airBoxSite = itemBody.airBoxSite.GetType().GetProperties().ToDictionary(x => x.Name, x => x.GetValue(itemBody.airBoxSite, null));

            var tasks = new List<Task<(string Index, string json)>>();

            foreach (var i in airBoxSite.Values)
            {
                Console.WriteLine("Starting Process {0}", i.ToString());
                tasks.Add(GetAirBoxInfo(i.ToString()));
            }
        }

        private async Task<(string Index, string json)> GetAirBoxInfo(string MAC)
        {
            AirBoxItem airboxItem = new AirBoxItem();

            var json = new WebClient().DownloadString(string.Format("https://pm25.lass-net.org/API-1.0.0/device/{0}/latest/?format=JSON", MAC));
            AirBoxFeed airBoxFeed = null;

            try
            {
                ItemResponse<AirBoxItem> airBoxResponse = await this.container.ReadItemAsync<AirBoxItem>(MAC, new PartitionKey(MAC));
                airBoxFeed = JsonSerializer.Deserialize<AirBoxFeed>(json);

                airboxItem.airbox = airBoxFeed;
                if(airboxItem.airbox.feeds.Count == 0) { airboxItem.siteName = airBoxResponse.Resource.siteName; }
                else airboxItem.siteName = airboxItem.airbox.feeds[0].AirBox.name;

                airboxItem.Id = MAC;
                airboxItem.PartitionKey = MAC;

                // Read the item to see if it exists.  
                ItemResponse<AirBoxItem> andersenFamilyResponse = await this.container.ReplaceItemAsync<AirBoxItem>(airboxItem, airboxItem.Id, new PartitionKey(airboxItem.PartitionKey));
                Console.WriteLine("Item in database with id: {0} updated.\n", andersenFamilyResponse.Resource.Id);
            }
            catch (JsonException)
            {
                //airboxItem.siteName = airBoxResponse.Resource.siteName;
                airboxItem.Id = MAC;
                airboxItem.PartitionKey = MAC;
                airboxItem.jsonIsBroken = true;

                ItemResponse<AirBoxItem> andersenFamilyResponse = await this.container.ReplaceItemAsync<AirBoxItem>(airboxItem, MAC, new PartitionKey(MAC));
                Console.WriteLine("Item in database with id: {0} updated with JSON Exception.\n", andersenFamilyResponse.Resource.Id);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                airBoxFeed = JsonSerializer.Deserialize<AirBoxFeed>(json);
                airboxItem.airbox = airBoxFeed;
                if (airboxItem.airbox.feeds.Count == 0) { airboxItem.siteName = ""; }
                else airboxItem.siteName = airboxItem.airbox.feeds[0].AirBox.name;
                airboxItem.Id = MAC;
                airboxItem.PartitionKey = MAC;

                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                ItemResponse<AirBoxItem> andersenFamilyResponse = await this.container.CreateItemAsync<AirBoxItem>(airboxItem, new PartitionKey(airboxItem.PartitionKey));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", andersenFamilyResponse.Resource.Id, andersenFamilyResponse.RequestCharge);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} \n {ex.StackTrace}");
            }

            return (MAC, json);
        }
    }
}
