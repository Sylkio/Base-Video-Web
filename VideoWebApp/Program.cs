using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using VideoWebapp.DbHelper;
using VideoWebApp.Data;
using VideoWebApp.Interface;
using VideoWebApp.Services;
using VideoWebapp.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 209715;
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VideoWebApp", Version = "v1" });
});

builder.Services.AddRazorPages();

// Register AzureService
builder.Services.AddScoped<IAzureService, AzureService>();
builder.Services.AddScoped<DbHelper>();

// Add CORS policy
builder.Services.AddCors(option => {
    option.AddPolicy("CorsVideoPolicy",
        policybuilder => policybuilder.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// Configure Entity Framework Core with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    options => options.EnableRetryOnFailure()));

// Configure Azure Blob Storage
var blobStorageConnectionString = builder.Configuration.GetConnectionString("BlobConnectionString");
builder.Services.AddSingleton(x => new BlobServiceClient(blobStorageConnectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VideoWebApp V1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseCors("CorsVideoPolicy");

app.MapControllers();
app.MapRazorPages();
app.MapHub<LivestreamHub>("/livestreamhub");

app.Run();
