using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Vagalume.Api.Client;
using Vagalume.Api.Contracts;
using Vagalume.Wasm.UI.Notes;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<NotesPage>("#app");
builder.Services.AddSingleton<INotesApi>(
    new NotesApiClient(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }));

await builder.Build().RunAsync();
