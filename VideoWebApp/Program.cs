using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using VideoWebApp.Data;
using VideoWebApp.Interface;
using VideoWebApp.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VideoWebApp", Version = "v1" });
});
// Add services to the container.
builder.Services.AddRazorPages();
// Register AzureService
builder.Services.AddScoped<IAzureService, AzureService>();
builder.Services.AddCors(option => {
    option.AddPolicy("CorsVideoPolicy",
        policybuilder => policybuilder.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});


// Database SQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// BlobServiceClient for Azure Blob Storage
var blobStorageConnectionString = builder.Configuration.GetConnectionString("BlobConnectionString");
builder.Services.AddSingleton(x => new BlobServiceClient(blobStorageConnectionString));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseCors("CorsVideoPolicy");
app.MapRazorPages();

app.Run();
