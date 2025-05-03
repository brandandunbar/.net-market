using BusinessLogic.Data;
using BusinessLogic.Logic;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WebApi.Dtos;
using WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configuraci�n de servicios
builder.Services.AddControllers();

// Configuraci�n de DbContexts
builder.Services.AddDbContext<MarketDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<SeguridadDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("IdentitySeguridad")));

// Configuraci�n de Identity
builder.Services.AddIdentityCore<Usuario>(options =>
{
    // Configuraci�n de opciones de contrase�a si es necesario
})
.AddEntityFrameworkStores<SeguridadDbContext>()
.AddSignInManager<SignInManager<Usuario>>();

// Configuraci�n de autenticaci�n JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Token:Key"])),
            ValidIssuer = builder.Configuration["Token:Issuer"],
            ValidateIssuer = true,
            ValidateAudience = false
        };
    });

// Configuraci�n de CORS
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("CorsRule", rule =>
    {
        rule.AllowAnyHeader()
             .AllowAnyMethod()
             .WithOrigins("http://localhost:5173") // Sin la barra final
             .AllowCredentials(); // Permite enviar cookies/token
    });
});

// Configuraci�n de AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfiles));

// Inyecci�n de dependencias
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddTransient<IProductoRepository, ProductoRepository>();

var app = builder.Build();

// Migraciones y seeding inicial
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();

    try
    {
        // Ejecutar migraciones para MarketDbContext
        var marketContext = services.GetRequiredService<MarketDbContext>();
        await marketContext.Database.MigrateAsync();
        await MarketDbContextData.CargarDataAsync(marketContext, loggerFactory);

        // Ejecutar migraciones para SeguridadDbContext
        var seguridadContext = services.GetRequiredService<SeguridadDbContext>();
        await seguridadContext.Database.MigrateAsync();

        // Seed de usuarios
        var userManager = services.GetRequiredService<UserManager<Usuario>>();
        await SeguridadDbContextData.SeedUserAsync(userManager);
    }
    catch (Exception ex)
    {
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogError(ex, "Error durante la migraci�n o seeding de datos");
    }
}

// Configuraci�n del pipeline HTTP
app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePagesWithReExecute("/errors", "?code={0}");

app.UseRouting();
app.UseCors("CorsRule");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();