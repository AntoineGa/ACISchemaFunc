using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerInstance;
using Microsoft.Azure.Management.ContainerInstance.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System.Configuration;


public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    await MainAsync();
    return req.CreateResponse(HttpStatusCode.OK);
}

private static async Task MainAsync()
{
    var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
    var client = new ContainerInstanceManagementClient(credentials);
    client.SubscriptionId = subscriptionId;

    var locations = new string[] { "EastUS", "WestUS", "WestEurope" }; // Currently available only three regions. 

    await CreateInstances(client, $"containerGroup", "WestEurope", 0, $"container-{1}");

    Console.WriteLine("Done. Press button");
    Console.ReadLine(); // Please click after finish the test.

    //await DeleteInstance(client, $"containerGroup");

    Console.WriteLine("Done. Deleted");
    Console.ReadLine();
}

private static async Task DeleteInstance(ContainerInstanceManagementClient client, string containerGroup)
{
    var logsList = client.ContainerLogs;
    var logs = logsList.List("acigroup", "containerGroup", "client");
    await client.ContainerGroups.DeleteAsync(resourceGroup, containerGroup);
}


private static async Task CreateInstances(ContainerInstanceManagementClient client, string containerGroup, string location, int flag, string guid)
{
    var resources = new ResourceRequirements();
    var request = new ResourceRequests();
    request.Cpu = 0.2;
    request.MemoryInGB = 0.3;
    resources.Requests = request;
    
    var list = new List<Container>();
    list.Add(CreateSchemaCrawlerContainer(containerName, resources, $"{guid}"));
    
    await client.ContainerGroups.CreateOrUpdateAsync(resourceGroup, containerGroup,
        new ContainerGroup()
        {
            Containers = list,
            Location = location,
            OsType = "Linux",
            RestartPolicy = "Never",   // try not to emit the message by restarting
            Volumes= new List<Volume>()
            {
                new Volume()
                {
                    Name = shareName,
                    AzureFile = new AzureFileVolume()
                    {
                        ShareName = shareName,
                        StorageAccountKey = shareAccountKey,
                        StorageAccountName = shareAccountName
                    }
                }
            }
        }
        );
    Console.WriteLine($"Done for {containerGroup}");
}



private static Container CreateSchemaCrawlerContainer(string name, ResourceRequirements resources, string guid)
{
    
    Console.WriteLine($"Create Container: {name}, DeviceID: {guid}, Connection:{ConfigurationManager.AppSettings.Get("StorageConnectionString")}");

    var list = new List<EnvironmentVariable>();
    
    list.Add(new EnvironmentVariable("DeviceID", guid));
    list.Add(new EnvironmentVariable("Password", password));
    list.Add(new EnvironmentVariable("Host", host));
    list.Add(new EnvironmentVariable("User", user));
    list.Add(new EnvironmentVariable("Database", database));
    list.Add(new EnvironmentVariable("Schema", schema));
    list.Add(new EnvironmentVariable("OutputFile", outputFile));

    var listCommand = new List<string>();
    listCommand.Add("/bin/sh");
    listCommand.Add("-c");
    listCommand.Add("./schemacrawler.sh -server=sqlserver -host=$Host -user=$User -password=$Password -database=$Database -schemas=$Schema -infolevel=schema -command=schema -outputformat=png -outputfile=/share/$OutputFile -loglevel=OFF");
    
    var listVolumeMount = new List<VolumeMount>();
    listVolumeMount.Add(new VolumeMount(shareName, "/share"));
    
    var container = new Container($"client", name, resources, environmentVariables: list, command: listCommand
        , volumeMounts: listVolumeMount
        );
    
    
    return container;

}

private static string resourceGroup = ConfigurationManager.AppSettings.Get("resourceGroup");
private static string containerGroup = ConfigurationManager.AppSettings.Get("containerGroup");
private static string subscriptionId = ConfigurationManager.AppSettings.Get("subscriptionId");
private static string clientId = ConfigurationManager.AppSettings.Get("clientId");
private static string clientSecret = ConfigurationManager.AppSettings.Get("clientSecret");
private static string tenantId = ConfigurationManager.AppSettings.Get("tenantId");
private static string shareName = ConfigurationManager.AppSettings.Get("shareName");
private static string shareAccountName = ConfigurationManager.AppSettings.Get("shareAccountName");
private static string shareAccountKey = ConfigurationManager.AppSettings.Get("shareAccountKey");
private static string password = ConfigurationManager.AppSettings.Get("dbPassword");
private static string host = ConfigurationManager.AppSettings.Get("dbHost");
private static string user = ConfigurationManager.AppSettings.Get("dbUser");
private static string database = ConfigurationManager.AppSettings.Get("dbName");
private static string schema = ConfigurationManager.AppSettings.Get("dbSchema");
private static string outputFile = ConfigurationManager.AppSettings.Get("outputFileName");

private static string containerName = "sualeh/schemacrawler";
