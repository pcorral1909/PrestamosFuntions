
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using PrestamosApiFunctions.Models.Request;
using PrestamosApiFunctions.Models.Response;
using Microsoft.VisualBasic;


namespace PrestamosApiFunctions.Functions.Clientes
{
    public class AltaClienteFunction
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;

        public AltaClienteFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AltaClienteFunction>();
            _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        }

        [Function("AltaCliente")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("llega al metodo");
            try
            {
                // Leer JSON
                var body = await req.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<AltaClienteRequest>(body);

                if (data == null || string.IsNullOrWhiteSpace(data.Nombre))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("El nombre del cliente es obligatorio.");
                    return bad;
                }

                int newId = 0;

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    var existsCmd = new SqlCommand(
                        "SELECT COUNT(1) FROM Clientes WHERE cliente = @Nombre",
                        conn);
                    existsCmd.Parameters.AddWithValue("@Nombre", data.Nombre);
                    var exists = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());

                    if (exists > 0)
                    {
                        var duplicateResponse = new AltaClienteResponse
                        {
                            ResponseBase = new ResponseBase
                            {
                                CodError = 1001,
                                Mensaje = "El cliente ya existe"
                            },
                            ClienteId = 0,
                            Nombre = data.Nombre
                        };

                        var resp = req.CreateResponse(HttpStatusCode.OK);
                        await resp.WriteAsJsonAsync(duplicateResponse);
                        return resp;
                    }

                    var insertCmd = new SqlCommand(
                        "INSERT INTO Clientes (cliente) VALUES (@Nombre); SELECT SCOPE_IDENTITY();",
                        conn);
                    insertCmd.Parameters.AddWithValue("@Nombre", data.Nombre);
                    newId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                }

                // Construir respuesta del API
                var apiResponse = new AltaClienteResponse
                {
                    ResponseBase = new ResponseBase
                    {
                        CodError = newId > 0 ? 0 : 1000,
                        Mensaje = newId > 0 ? "Cliente agregado correctamente" : "No se pudo insertar el cliente"
                    },
                    ClienteId = newId,
                    Nombre = data.Nombre
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(apiResponse);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AltaCliente");

                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("Error interno.");
                return error;
            }
        }
    }
   
    
   
}
