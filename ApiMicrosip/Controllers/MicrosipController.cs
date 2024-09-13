using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Runtime.InteropServices;
using System.Text;

namespace ApiMicrosip.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MicrosipController : ControllerBase
    {
        public const string DllPath = "..\\ApiMicrosip.dll";

        private string connectionString = "User=SYSDBA;Password=masterkey;Database=\"C:\\Microsip datos\\SUPER CARQUIN.FDB\";DataSource=server;Port=3050;Dialect=3;Charset=UTF8;";


        [DllImport(DllPath, SetLastError = true)]
        public static extern int NewDB();

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetErrorHandling(int eType, int eLevel);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int NewTrn(int db, int op);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int DBConnect(int db, string path, string user, string pass);

        [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetDBInventarios(int dbHandle);

        [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetDBVentas(int dbHandle);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetLastErrorMessage(StringBuilder msg);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DBDisconnect(int db);

        [DllImport(DllPath, SetLastError = true)]
        public static extern int NuevaSalida(int ConceptoInId, int AlmacenId, int AlmacenDestinoId,
                                     string Fecha, string Folio, string Descripcion, int CentroCostoId);

        [DllImport(DllPath, SetLastError = true)]
        public static extern int AplicaSalida();

        [DllImport(DllPath, SetLastError = true)]
        public static extern int RenglonSalida(int ArticuloId, double Unidades, double CostoUnitario, double CostoTotal);

        [DllImport(DllPath, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int NuevoPedido(string Fecha, string Folio, int ClienteId, int DirConsigId, int AlmacenId,
    string FechaEntrega, string TipoDscto, double Descuento, string OrdenCompra, string Descripcion,
    int VendedorId, int ImptoSustituidoId, int ImptoSustitutoId, int MonedaId);

        [DllImport(DllPath, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int RenglonPedido(int ArticuloId, double Unidades, double PrecioUnitario, double PctjeDscto, string Notas);

        [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
        public static extern int AplicaPedido();



        [HttpPost("connect")]
        public ActionResult<string> ConnectToMicrosip([FromBody] ConexionMicrosipRequest request)
        {
            int dbHandle = NewDB();
            SetErrorHandling(0, 0);
            int trn = NewTrn(dbHandle, 3);


            string connectionString = $"{request.Server}:{request.DatabasePath}";
            int conecta = DBConnect(dbHandle, connectionString, request.Username, request.Password);

            if (conecta != 0)
            {
                StringBuilder obtieneError = new StringBuilder(1000);
                GetLastErrorMessage(obtieneError);
                return BadRequest($"Error al conectar: {obtieneError.ToString()}");
            }

            DBDisconnect(dbHandle);


            return Ok("Conexión exitosa y SetDBInventarios ejecutado correctamente.");

        }


        [HttpGet("datos")]
        public IActionResult ObtenerDatos()
        {
            string queryConceptos = "SELECT * FROM CONCEPTOS_IN";
            string queryAlmacenes = "SELECT * FROM ALMACENES";

            DataSet dataSetConceptos = new DataSet();
            DataSet dataSetAlmacenes = new DataSet();

            try
            {
                using (FbConnection connection = new FbConnection(connectionString))
                {
                    connection.Open();

                    using (FbDataAdapter adapterConceptos = new FbDataAdapter(queryConceptos, connection))
                    {
                        adapterConceptos.Fill(dataSetConceptos, "CONCEPTOS_IN");
                    }

                    using (FbDataAdapter adapterAlmacenes = new FbDataAdapter(queryAlmacenes, connection))
                    {
                        adapterAlmacenes.Fill(dataSetAlmacenes, "ALMACENES");
                    }
                }

                var conceptosList = ConvertDataTableToList(dataSetConceptos.Tables["CONCEPTOS_IN"]);
                var almacenesList = ConvertDataTableToList(dataSetAlmacenes.Tables["ALMACENES"]);

                var result = new
                {
                    Conceptos = conceptosList,
                    Almacenes = almacenesList
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al conectar a la base de datos: {ex.Message}");
            }
        }

        private List<Dictionary<string, object>> ConvertDataTableToList(DataTable dataTable)
        {
            var list = new List<Dictionary<string, object>>();

            foreach (DataRow row in dataTable.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    dict[col.ColumnName] = row[col];
                }
                list.Add(dict);
            }

            return list;
        }

        [HttpPost("obtener-articulo")]
        public ActionResult<ArticuloResponse> ObtenerArticuloPorClave([FromBody] ArticuloRequestt request)
        {
            if (string.IsNullOrEmpty(request.ClaveArticulo))
            {
                return BadRequest("La clave del artículo es requerida.");
            }

            int articuloId = 0;
            string nombreArticulo = "";

            try
            {
                using (FbConnection connection = new FbConnection(connectionString))
                {
                    connection.Open();

                    string query = @"SELECT DISTINCT A.ARTICULO_ID, A.NOMBRE
                                 FROM ARTICULOS A
                                 INNER JOIN CLAVES_ARTICULOS C ON A.ARTICULO_ID = C.ARTICULO_ID
                                 WHERE C.CLAVE_ARTICULO = @ClaveArticulo";

                    using (FbCommand command = new FbCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ClaveArticulo", request.ClaveArticulo);

                        using (FbDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                articuloId = reader.GetInt32(reader.GetOrdinal("ARTICULO_ID"));
                                nombreArticulo = reader["NOMBRE"].ToString();
                            }
                            else
                            {
                                return NotFound("Artículo no encontrado.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error al obtener el artículo: " + ex.Message);
            }

            var response = new ArticuloResponse
            {
                ArticuloId = articuloId,
                NombreArticulo = nombreArticulo
            };

            return Ok(response);
        }

        [HttpPost("generar-salida")]
        public async Task<IActionResult> GenerarSalida([FromBody] SalidaRequest request)
        {
            
            string server = "SERVER"; 
            string databasePath = "C:\\Microsip datos\\SUPER CARQUIN.FDB"; 
            string username = "SYSDBA"; 
            string password = "masterkey"; 

            string connectionString = $"{server}:{databasePath}";

            int dbHandle = NewDB();
            SetErrorHandling(0, 0);
            int trn = NewTrn(dbHandle, 3);

            int conecta = DBConnect(dbHandle, connectionString, username, password);

            if (conecta != 0)
            {
                StringBuilder obtieneError = new StringBuilder(1000);
                GetLastErrorMessage(obtieneError);
                return BadRequest($"Error al conectar: {obtieneError.ToString()}");
            }
            int setDbResult = SetDBInventarios(dbHandle);
            if (setDbResult != 0)
            {
                return BadRequest($"SetDBInventarios: Error {setDbResult}");
            }

            try
            {
                int resultadoNuevaSalida = NuevaSalida(request.ConceptoId, request.AlmacenOrigenId, request.AlmacenDestinoId,
                                                        request.Fecha.ToString("d/M/yyyy"), request.Folio, request.Descripcion, request.CentroCostoId);

                if (resultadoNuevaSalida == 0) 
                {
                    foreach (var articulo in request.Articulos)
                    {
                        int articuloId = articulo.ArticuloId; 

                        double unidades = articulo.Unidades;

                        int resultadoRenglonSalida = RenglonSalida(articuloId, unidades, 0.0, 0.0); 

                        if (resultadoRenglonSalida != 0) 
                        {
                            return StatusCode(500, $"Error al agregar el renglón de salida. Código de error: {resultadoRenglonSalida}");
                        }
                    }

                    int resultadoAplicaSalida = AplicaSalida();

                    if (resultadoAplicaSalida == 0) 
                    {
                        return Ok("Salida generada y aplicada exitosamente.");
                    }
                    else
                    {
                        return StatusCode(500, $"Error al aplicar la salida. Código de error: {resultadoAplicaSalida}");
                    }
                }
                else
                {
                    return StatusCode(500, $"Error al generar la salida. Código de error: {resultadoNuevaSalida}");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
            finally
            {

            }
        }
        [HttpPost("generar-pedido")]
        public async Task<IActionResult> GenerarPedido([FromBody] PedidoRequest request)
        {
            string server = "SERVER";
            string databasePath = "C:\\Microsip datos\\SUPER CARQUIN.FDB";
            string username = "SYSDBA";
            string password = "masterkey";

            string connectionString = $"{server}:{databasePath}";

            int dbHandle = NewDB();
            SetErrorHandling(0, 0);
            int trn = NewTrn(dbHandle, 3);

            int conecta = DBConnect(dbHandle, connectionString, username, password);

            if (conecta != 0)
            {
                StringBuilder obtieneError = new StringBuilder(1000);
                GetLastErrorMessage(obtieneError);
                return BadRequest($"Error al conectar: {obtieneError.ToString()}");
            }
            int setDbResult = SetDBVentas(dbHandle);
            if (setDbResult != 0)
            {
                return BadRequest($"SetDBVentas: Error {setDbResult}");
            }

            try
            {
                // Asegúrate de que la fecha se convierta al formato de cadena necesario
                string fechaPedido = request.Fecha?.ToString("dd/MM/yyyy");
                string fechaEntrega = request.FechaEntrega?.ToString("dd/MM/yyyy");

                if (string.IsNullOrEmpty(fechaPedido) || string.IsNullOrEmpty(fechaEntrega))
                {
                    return BadRequest("Error: La fecha del pedido o la fecha de entrega no son válidas.");
                }

                int resultadoNuevoPedido = NuevoPedido(
                    fechaPedido,
                    request.Folio,
                    request.ClienteId,
                    request.DirConsigId,
                    request.AlmacenId,
                    fechaEntrega,
                    request.TipoDscto,
                    request.Descuento,
                    request.OrdenCompra,
                    request.Descripcion,
                    request.VendedorId,
                    request.ImptoSustituidoId,
                    request.ImptoSustitutoId,
                    request.MonedaId
                );

                if (resultadoNuevoPedido == 0)
                {
                    foreach (var articulo in request.Articulos)
                    {
                        int articuloId = articulo.ArticuloId;
                        double unidades = articulo.Unidades;
                        double precioUnitario = articulo.PrecioUnitario;
                        double pctjeDscto = articulo.PctjeDscto;
                        string notas = articulo.Notas;

                        int resultadoRenglonPedido = RenglonPedido(articuloId, unidades, precioUnitario, pctjeDscto, notas);

                        if (resultadoRenglonPedido != 0)
                        {
                            return StatusCode(500, $"Error al agregar el renglón del pedido. Código de error: {resultadoRenglonPedido}");
                        }
                    }

                    int resultadoAplicaPedido = AplicaPedido();

                    if (resultadoAplicaPedido == 0)
                    {
                        return Ok("Pedido generado y aplicado exitosamente.");
                    }
                    else
                    {
                        return StatusCode(500, $"Error al aplicar el pedido. Código de error: {resultadoAplicaPedido}");
                    }
                }
                else
                {
                    return StatusCode(500, $"Error al generar el pedido. Código de error: {resultadoNuevoPedido}");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
            finally
            {
                // Aquí puedes cerrar la conexión o manejar cualquier limpieza necesaria
            }
        }


    }
    public class PedidoRequest
    {
        public DateTime? Fecha { get; set; }
        public string Folio { get; set; } // Folio del pedido
        public int ClienteId { get; set; } // ID del cliente
        public int DirConsigId { get; set; } // ID de la dirección de consignación
        public int AlmacenId { get; set; } // ID del almacén
        public DateTime? FechaEntrega { get; set; } // Fecha de entrega
        public string TipoDscto { get; set; } // Tipo de descuento
        public double Descuento { get; set; } // Descuento aplicado
        public string OrdenCompra { get; set; } // Orden de compra
        public string Descripcion { get; set; } // Descripción del pedido
        public int VendedorId { get; set; } // ID del vendedor
        public int ImptoSustituidoId { get; set; } // ID del impuesto sustituido
        public int ImptoSustitutoId { get; set; } // ID del impuesto sustituto
        public int MonedaId { get; set; } // ID de la moneda
        public List<ArticuloPedido> Articulos { get; set; } // Lista de artículos en el pedido
    }

    public class ArticuloPedido
    {
        public int ArticuloId { get; set; } // ID del artículo
        public double Unidades { get; set; } // Cantidad de unidades
        public double PrecioUnitario { get; set; } // Precio unitario del artículo
        public double PctjeDscto { get; set; } // Porcentaje de descuento aplicado
        public string Notas { get; set; } // Notas adicionales sobre el artículo
    }


    public class SalidaRequest
    {
        public int ConceptoId { get; set; }
        public int AlmacenOrigenId { get; set; }
        public int AlmacenDestinoId { get; set; }
        public DateTime Fecha { get; set; }
        public string Folio { get; set; }
        public string Descripcion { get; set; }
        public int CentroCostoId { get; set; }
        public ArticuloRequest[] Articulos { get; set; }
    }

    public class ArticuloRequest
    {
        public int ArticuloId { get; set; } // Actualizado para utilizar el ID del artículo
        public double Unidades { get; set; }
    }

    public class ArticuloRequestt
    {
        public string ClaveArticulo { get; set; }
    }




        public class Renglon
        {
            public int ArticuloId { get; set; }
            public double Unidades { get; set; }
            public double CostoUnitario { get; set; }
            public double CostoTotal { get; set; }
        }


        //clase para la solicitud de artiuclo por clave 

        public class ArticuloResponse
        {
            public int ArticuloId { get; set; }
            public string NombreArticulo { get; set; }
        }


        // Clase para la solicitud de conexión
        public class ConexionMicrosipRequest
        {
            public string Server { get; set; }
            public string DatabasePath { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }

