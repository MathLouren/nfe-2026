using NFE.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );

            var response = new
            {
                sucesso = false,
                mensagem = "Dados inválidos",
                erros = errors
            };

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "API NFS-e - Sistema Nacional 2026",
        Version = "v1",
        Description = "API para emissão, consulta, cancelamento e substituição de Notas Fiscais de Serviço Eletrônicas (NFS-e) conforme padrão nacional 2026 e leiautes-NSF-e.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Sistema NFS-e",
            Email = "suporte@exemplo.com.br"
        }
    });

    // Incluir comentários XML
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Register services - Padrão MVC
builder.Services.AddHttpClient<WebServiceClient>();
builder.Services.AddScoped<IWebServiceClient, WebServiceClient>();
builder.Services.AddScoped<INFeService, NFeService>();

// Register NFS-e services
builder.Services.AddHttpClient<NFSeWebServiceClient>();
builder.Services.AddScoped<INFSeWebServiceClient, NFSeWebServiceClient>();

// Sistema Nacional NFS-e (Sefin Nacional)
builder.Services.AddHttpClient<SistemaNacionalNFSeClient>();
builder.Services.AddScoped<SistemaNacionalNFSeClient>();

// DPS Service (geração de XML DPS conforme leiautes-NSF-e)
builder.Services.AddScoped<DPSService>();

// Evento Service (geração de XML de eventos)
builder.Services.AddScoped<EventoNFSeService>();

// Validador XSD
builder.Services.AddScoped<ValidadorXSDService>();

// NFSe Service (usa DPS e Sistema Nacional)
builder.Services.AddScoped<INFSeService, NFSeService>();

// Shared services
builder.Services.AddScoped<AssinaturaDigital>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map controllers
app.MapControllers();

app.Run();

