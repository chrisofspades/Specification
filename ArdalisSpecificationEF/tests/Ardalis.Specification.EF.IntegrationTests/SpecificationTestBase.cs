using Ardalis.Specification.EntityFrameworkCore.IntegrationTests.Data;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Ardalis.Specification.EntityFrameworkCore.IntegrationTests
{
    public class SpecificationTestBase
    {
        // Docker
        public const string ConnectionString = "Data Source=localhost,14330;Initial Catalog=SampleDatabase;PersistSecurityInfo=True;User ID=sa;Password=P@ssW0rd!";
        public const string DbContainerName = "databaseEF";
        // (localdb)
        //public const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Integrated Security=SSPI;Initial Catalog=SpecificationEFTestsDB;";

        protected TestDbContext dbContext;
        protected Repository<Company> companyRepository;
        protected Repository<Store> storeRepository;

        private DockerClient _dockerClient;

        public SpecificationTestBase()
        {
            _dockerClient = new DockerClientConfiguration().CreateClient();
            
            if (HasContainer(DbContainerName) && !ContainerIsRunning(DbContainerName))
            {
                StartContainer(DbContainerName);
            }
            else if (!HasContainer(DbContainerName))
            {
                CreateContainer(DbContainerName);
                StartContainer(DbContainerName);
            }


            var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
            optionsBuilder.UseSqlServer(ConnectionString);
            dbContext = new TestDbContext(optionsBuilder.Options);

            dbContext.Database.EnsureCreated();

            companyRepository = new Repository<Company>(dbContext);
            storeRepository = new Repository<Store>(dbContext);
        }

        private void CreateContainer(string containerName)
        {
            string repo = "mcr.microsoft.com/mssql/server";
            string tag = "2019-CU3-ubuntu-18.04";
            string image = $"{repo}:{tag}";

            _dockerClient.Images.CreateImageAsync(new ImagesCreateParameters
            {
                FromImage = repo,
                Tag = tag,
            }, null, new Progress<JSONMessage>()).GetAwaiter().GetResult();

            var response = _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = image,
                Name = containerName,
                Env = new List<string> { "SA_PASSWORD=P@ssW0rd!", "ACCEPT_EULA=Y" },
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    { "1433/tcp", new EmptyStruct() }
                },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        { "1433/tcp", new PortBinding[] { new PortBinding { HostPort = "14330" } } }
                    }
                }
            }).GetAwaiter().GetResult();
            Console.WriteLine($"{containerName} container created.");
        }

        private bool ContainerIsRunning(string containerName)
        {
            return HasContainer(containerName, false);
        }

        private bool HasContainer(string containerName, bool all = true)
        {
            var containers = _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = all,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "name", new Dictionary<string, bool> { { containerName, true } } }
                }
            }).GetAwaiter().GetResult();

            bool hasContainer = containers.Count > 0;
            return hasContainer;
        }

        private void StartContainer(string id)
        {
            _dockerClient.Containers.StartContainerAsync(id, new ContainerStartParameters()).GetAwaiter().GetResult();

            // Wait for sql server to warm up
            Task.Delay(15000).GetAwaiter().GetResult();

            Console.WriteLine($"{id} container started.");
        }
    }
}
