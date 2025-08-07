using asuncion_cardano_api.Models;
using asuncion_cardano_api.Services;
using AsuncionCardanoApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<ValidatorService>();
builder.Services.AddScoped<CardanoTransactionService>();
builder.Services.AddScoped<CardanoPlutusTransactionService>();
builder.Services.AddScoped<DatumBuilderService>();
builder.Services.AddScoped<LockActaService>();
builder.Services.AddScoped<UtxoFinderService>();
builder.Services.Configure<CardanoSettings>(
builder.Configuration.GetSection("Cardano"));




var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
