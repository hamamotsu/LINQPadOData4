using System;
using System.Net;
using System.Net.Http;
using System.Text;
using LINQPad.Extensibility.DataContext;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;
using OData4.LINQPadDriver.Templates;

namespace OData4.LINQPadDriver
{
	public class DynamicDriver : DynamicDataContextDriver
	{
		private const string BaseNamespacePrefix = "LINQPad.User";

		/// <summary> Assemblies, using both to compile generated code and to load into LINQPad </summary>
		private static readonly string[] Assemblies =
		{
			"Microsoft.OData.Client.dll",
			"Microsoft.OData.Core.dll",
			"Microsoft.OData.Edm.dll",
			"Microsoft.Spatial.dll",
		};

		private List<string> _namespaces;

		public override string Name => "OData v4 Connection (.NET 10)";

		public override string Author => "Dmitrii Smirnov, hamamotsu";

		public override string GetConnectionDescription(IConnectionInfo connectionInfo)
		{
			return connectionInfo.GetConnectionProperties().Uri;
		}

		public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo connectionInfo)
		{
			// We need to pass the chosen URI into the DataServiceContext's constructor:
			return new[] { new ParameterDescriptor("serviceRoot", "System.Uri") };
		}

		public override object[] GetContextConstructorArguments(IConnectionInfo connectionInfo)
		{
			// We need to pass the chosen URI into the DataServiceContext's constructor:
			return new object[] { new Uri(connectionInfo.GetConnectionProperties().Uri) };
		}

		public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo connectionInfo)
		{
			// We need the following assembly for compilation and auto-completion:
			return Assemblies;
		}

		public override IDbConnection GetIDbConnection(IConnectionInfo connectionInfo)
		{
			return null;
		}

		public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo connectionInfo)
		{
			if (_namespaces == null || _namespaces.Count == 0)
			{
				var model = connectionInfo.GetConnectionProperties().GetModel();
				var namespaces = model.DeclaredNamespaces.Select(o => BaseNamespacePrefix + "." + o).ToList();
				_namespaces = namespaces.Count > 1 ? namespaces.ToList() : new List<string>(1);
				_namespaces.Add("Microsoft.OData.Client");
			}

			return _namespaces;
		}

		public override bool ShowConnectionDialog(IConnectionInfo connectionInfo, ConnectionDialogOptions dialogOptions)
		{
			var connectionProperties = connectionInfo.GetConnectionProperties();

			// Populate the default URI with a demo data:
			if (dialogOptions.IsNewConnection)
				connectionProperties.Uri = "https://services.odata.org/TripPinRESTierService";

			return new ConnectionDialog(connectionProperties).ShowDialog() == true;
		}

		public override void PreprocessObjectToWrite(ref object objectToWrite, ObjectGraphInfo info)
		{
			if (objectToWrite is DataServiceQuery dataServiceQuery)
			{
				objectToWrite = dataServiceQuery.ExecuteAsync().GetAwaiter().GetResult();
			}
		}

		// ReSharper disable once RedundantAssignment
		public override List<ExplorerItem> GetSchemaAndBuildAssembly(IConnectionInfo connectionInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
		{
			var properties = connectionInfo.GetConnectionProperties();

			var codeGenerator = new ODataT4CodeGenerator
			{
				MetadataDocumentUri = properties.Uri,
				NamespacePrefix = nameSpace,
				TargetLanguage = ODataT4CodeGenerator.LanguageOption.CSharp,
				UseDataServiceCollection = false,
				EnableNamingAlias = false,
				IgnoreUnexpectedElementsAndAttributes = true,
				Properties = properties,
			};
			var code = codeGenerator.TransformText();

			BuildAssembly(code, assemblyToBuild, connectionInfo);

			var model = properties.GetModel();

			typeName = GetContainerName(model);
			var schema = model.GetSchema();

			return schema;
		}

		private static void BuildAssembly(string code, AssemblyName assemblyToBuild, IConnectionInfo connectionInfo)
		{
			var assemblies = new List<string>
			{
				typeof(IEdmModel).Assembly.Location,
				typeof(DataServiceQuery).Assembly.Location,
				typeof(Microsoft.Spatial.Geometry).Assembly.Location,
			};

			var references = GetCoreFxReferenceAssemblies(connectionInfo)
				.Concat(assemblies)
				.Select(x => MetadataReference.CreateFromFile(x));

			var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

			var compilation = CSharpCompilation
				.Create(assemblyToBuild.FullName)
				.WithOptions(options)
				.AddReferences(references)
				.AddSyntaxTrees(CSharpSyntaxTree.ParseText(code));

#pragma warning disable SYSLIB0044
			using var fileStream = File.Create(assemblyToBuild.CodeBase);
#pragma warning restore SYSLIB0044

			var results = compilation.Emit(fileStream);

			if (results.Success)
			{
				return;
			}

			var msg = results
				.Diagnostics
				.Where(d => d.Severity == DiagnosticSeverity.Error)
				.Aggregate("Can't compile typed context:", (s, e) => s + Environment.NewLine + e.GetMessage());

			throw new Exception(msg);
		}

		/// <summary> Get main schema container name for given service uri </summary>
		/// <param name="model">Entity Data Model</param>
		/// <returns>Container name</returns>
		private static string GetContainerName(IEdmModel model)
		{
			var root = model.EntityContainer;

			// Count namespaces from all schema elements in the model tree,
			// matching the T4 template's NamespacesInModel logic.
			var allNamespaces = GetNamespacesFromModelTree(model);
			var containerName = allNamespaces.Count > 1 ? root.FullName() : root.Name;

			return containerName;
		}

		private static HashSet<string> GetNamespacesFromModelTree(IEdmModel model)
		{
			var namespaces = new HashSet<string>(model.SchemaElements.Select(e => e.Namespace));

			foreach (var referenced in model.ReferencedModels)
			{
				if (referenced is Microsoft.OData.Edm.EdmCoreModel)
					continue;

				// Skip well-known vocabulary models (same filter as T4 template)
				if (referenced.FindDeclaredTerm("Org.OData.Core.V1.OptimisticConcurrency") != null ||
					referenced.FindDeclaredTerm("Org.OData.Capabilities.V1.ChangeTracking") != null ||
					referenced.FindDeclaredTerm("Org.OData.Core.V1.AlternateKeys") != null ||
					referenced.FindDeclaredTerm("Org.OData.Authorization.V1.Authorizations") != null ||
					referenced.FindDeclaredTerm("Org.OData.Validation.V1.DerivedTypeConstraint") != null ||
					referenced.FindDeclaredTerm("Org.OData.Community.V1.UrlEscapeFunction") != null)
					continue;

				foreach (var e in referenced.SchemaElements)
					namespaces.Add(e.Namespace);
			}

			return namespaces;
		}

		private static HttpClientHandler CreateHandler(ConnectionProperties properties)
		{
			var handler = new HttpClientHandler();

			handler.Proxy = properties.GetWebProxy();
			handler.UseProxy = true;

			var credentials = properties.GetCredentials();
			if (credentials != null)
			{
				handler.Credentials = credentials;
				handler.PreAuthenticate = true;
			}

			var cert = properties.GetClientCertificate();
			if (cert != null)
			{
				handler.ClientCertificates.Add(cert);
			}

			if (properties.AcceptInvalidCertificate)
			{
				handler.ServerCertificateCustomValidationCallback =
					HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
			}

			return handler;
		}

		public override void InitializeContext(IConnectionInfo connectionInfo, object context, QueryExecutionManager executionManager)
		{
			var dsContext = (DataServiceContext)context;

			var properties = connectionInfo.GetConnectionProperties();

			// Set IHttpClientFactory on DataServiceContext
			var services = new ServiceCollection();
			services.AddHttpClient(string.Empty)
				.ConfigurePrimaryHttpMessageHandler(() => CreateHandler(properties));
			var serviceProvider = services.BuildServiceProvider();
			dsContext.HttpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

			var writer = executionManager.SqlTranslationWriter;

			// SendingRequest2: custom headers, Basic auth header, and logging
			dsContext.SendingRequest2 += (s, e) =>
			{
				// Custom headers
				foreach (var kv in properties.CustomHeaders)
					e.RequestMessage.SetHeader(kv.Key, kv.Value);

				// Basic auth header
				if (properties.AuthenticationType == AuthenticationType.Basic
					&& !string.IsNullOrEmpty(properties.UserName))
				{
					var basic = Convert.ToBase64String(
						Encoding.ASCII.GetBytes($"{properties.UserName}:{properties.Password}"));
					e.RequestMessage.SetHeader("Authorization", $"Basic {basic}");
				}

				// Logging
				if (writer == null)
					return;

				writer.WriteLine($"URL:\t\t{e.RequestMessage.Url}");

				if (properties.LogMethod)
					writer.WriteLine($"Method:\t{e.RequestMessage.Method}");

				if (properties.LogHeaders)
				{
					writer.WriteLine("Headers:");
					var headers = string.Join("\r\n", e.RequestMessage.Headers.Select(o => $"\t{o.Key}:{o.Value}"));
					writer.WriteLine(headers);
				}
			};
		}

		public override bool AreRepositoriesEquivalent(IConnectionInfo r1, IConnectionInfo r2)
		{
			return Equals(r1.DriverData.Element("Uri"), r2.DriverData.Element("Uri"));
		}
	}
}
