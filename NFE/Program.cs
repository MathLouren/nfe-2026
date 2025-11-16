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
builder.Services.AddSwaggerGen();

// Register services - Padrão MVC
builder.Services.AddHttpClient<WebServiceClient>();
builder.Services.AddScoped<IWebServiceClient, WebServiceClient>();
builder.Services.AddScoped<INFeService, NFeService>();
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

