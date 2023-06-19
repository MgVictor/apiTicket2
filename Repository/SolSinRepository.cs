using apiTicket.Models;
using apiTicket.Models.Reportes;
using apiTicket.Repository.DB;
using apiTicket.Repository.Interfaces;
using apiTicket.Utils;
using apiTicket.Utils.Authentication;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using Oracle.ManagedDataAccess.Client;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Numeric;

namespace apiTicket.Repository
{
    public class SolSinRepository : ISolSinRepository
    {
        private readonly IOptions<AppSettings> appSettings;
        private readonly IConnectionBase _connectionBase;

        public SolSinRepository(IOptions<AppSettings> appSettings, IConnectionBase ConnectionBase)
        {
            this.appSettings = appSettings;
            _connectionBase = ConnectionBase;
        }

        private string Package3 = "PKG_BDU_CLIENT_360";
        private string Package4 = "PKG_BDU_TICKET";

        public string Codificatiempo()
        {
            DateTime tiempo = DateTime.Now;
            return tiempo.Year.ToString()
                + tiempo.Month.ToString()
                + tiempo.Day.ToString()
                + tiempo.Hour.ToString()
                + tiempo.Minute.ToString()
                + tiempo.Millisecond.ToString();
        }

        /*MÉTODOS*/
        public Ab_Adjunto S3Adjuntar(Ab_Adjunto ar, string type, string customfield)
        {
            //string base6 = "";
            var client = new RestClient(
                AppSettings.AWSAdjuntar
                    + ar.name
                    + "&time="
                    + Codificatiempo()
                    + "&field="
                    + customfield
            );
            string responsed = "";
            string token = new TokenService().getTokenAWS(type);
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", "Bearer " + token);
            request.AddHeader("Content-Type", ar.mime);
            request.AddParameter(
                ar.name,
                Convert.FromBase64String(ar.content),
                ParameterType.RequestBody
            );
            try
            {
                IRestResponse response = client.Execute(request);
                ID respuesta = JsonConvert.DeserializeObject<ID>(response.Content);

                ar.path_gd = respuesta.id;
            }
            catch (Exception ex)
            {
                responsed = ex.Message;
            }
            return ar;
        }

        public List<EstructuraTicket> GetEstructura(string Tipo)
        {
            List<EstructuraTicket> response = new List<EstructuraTicket>();
            try
            {
                List<OracleParameter> parameters = new List<OracleParameter>();
                parameters.Add(
                    new OracleParameter(
                        "TIPO",
                        OracleDbType.Varchar2,
                        Tipo,
                        ParameterDirection.Input
                    )
                );
                parameters.Add(
                    new OracleParameter("RC1", OracleDbType.RefCursor, ParameterDirection.Output)
                );
                using (
                    OracleDataReader dr = (OracleDataReader)
                        _connectionBase.ExecuteByStoredProcedure(
                            string.Format("{0}.{1}", Package3, "PRC_GET_ESTRUCTURAJIRA"),
                            parameters,
                            ConnectionBase.enuTypeDataBase.OracleVTime
                        )
                )
                {
                    while (dr.Read())
                    {
                        EstructuraTicket est = new EstructuraTicket();
                        est.SCODE_JIRA = dr["SCODE_JIRA"].ToString();
                        est.SDESCRIPT = dr["SDESCRIPT"].ToString();
                        est.STYPE = dr["STYPE"].ToString();
                        est.NREQUIRED = dr["NREQUIRED"].ToString();
                        response.Add(est);
                    }
                }
            }
            catch (Exception)
            {
                response = new List<EstructuraTicket>();
            }
            return response;
        }

        public Ab_RegContractRes RegistraTicketJIRAAsync(Ab_TicketDinamico request, string type)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logFilePath = Path.Combine(baseDirectory, "logfile.txt");

            try
            {
                string bass = AppSettings.AWSRegistrar;
                string token = new TokenService().getTokenAWS(type);
                var result = PostRequest(bass, AppSettings.AWSRegistrar2, request, token);
                LogToFile(logFilePath, $"Ya sali del post request: {result}");

                ID res = null;
                try
                {
                    if (result != null)
                    {
                        LogToFile(logFilePath, $"Antes de convertir");
                        res = JsonConvert.DeserializeObject<ID>(result);
                        if (res == null)
                        {
                            throw new Exception("El resultado de deserialización es nulo.");
                        }
                    }
                    else
                    {
                        // Manejar el caso en el que result es null
                        // Puedes lanzar una excepción, mostrar un mensaje de error, etc.
                        throw new Exception("El resultado de la solicitud es nulo.");
                    }
                }
                catch (JsonException ex)
                {
                    LogToFile(
                        logFilePath,
                        $"Error al deserializar el resultado: {ex.Message}\nResultado: {result}"
                    );
                    throw; // Puedes decidir lanzar la excepción para capturarla en un nivel superior o manejarla aquí mismo.
                }

                //PRODUCCION

                if (res != null)
                {
                    Ab_RegContractRes response = new Ab_RegContractRes
                    {
                        Codigo = res.id,
                        respuesta = true,
                        mensaje = "Registro Exitoso"
                    };

                    LogToFile(logFilePath, $"Registro exitoso - Código: {res.id}");
                    return response;
                }
                else
                {
                    Ab_RegContractRes response = new Ab_RegContractRes
                    {
                        Codigo = null, // Valor predeterminado para el código en caso de error
                        respuesta = false, // Valor predeterminado para la respuesta en caso de error
                        mensaje = "Error al deserializar el resultado JSON"
                    };
                    return response;
                }
            }
            catch (Exception ex)
            {
                LogToFile(logFilePath, $"Error: {ex.Message}\nStack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private void LogToFile(string logFilePath, string v)
        {
            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {v}");
            }
        }

        public string PostRequest(
            string baseUrl,
            string url,
            object postObject,
            string token
            )
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logFilePath = Path.Combine(baseDirectory, "logfile.txt");

            string result = null;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                UriBuilder builder = new UriBuilder(baseUrl + url);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    if (token != null)
                    {
                        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                    }
                    LogToFile(logFilePath, $"Token: {token}");
                    LogToFile(logFilePath, $"JSON: {postObject}");

                    var json = JsonConvert.SerializeObject(postObject);
                    //var cadena = "{\"system\":\"cliente360\"}";
                    //string json = JsonConvert.SerializeObject(cadena);

                    LogToFile(logFilePath, $"JSON CONVERTIDO: {json}");
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var stringContent = new StringContent(
                        json,
                        UnicodeEncoding.UTF8,
                        "application/json"
                    );
                    // var stringContent = new StringContent(json);
                    LogToFile(logFilePath, $"STRING CONTENT: {stringContent}");
                    var response = client.PostAsync(builder.Uri, stringContent).Result;

                    LogToFile(logFilePath, $"Despues del response");
                    result = response.Content.ReadAsStringAsync().Result; //JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result);
                }
            }
            catch (Exception)
            {
                result = "";
            }
            LogToFile(logFilePath, $"Resultado: {result}");
            return result;
        }

        public async Task<string> PostRequestJson(
            string baseUrl,
            string url,
            string serializedJson,
            string token
        )
        {
            string result = null;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                UriBuilder builder = new UriBuilder(baseUrl + url);
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                    //client.DefaultRequestHeaders.Clear();
                    //if (token != null)
                    //{
                    //    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                    //}
                    string json = JsonConvert.SerializeObject(serializedJson);
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var stringContent = new StringContent(
                        json,
                        UnicodeEncoding.UTF8,
                        "application/json"
                    );
                    var response = client.PostAsync(builder.Uri, stringContent).Result;

                    result = response.Content.ReadAsStringAsync().Result; //JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result);
                }
            }
            catch (Exception)
            {
                result = "";
            }

            return result;
        }

        public Ab_RegContractRes SetJIRA(Ticket ticket)
        {
            Ab_RegContractRes response = new Ab_RegContractRes();

            string pak = "PKG_BDU_CLIENT_360";
            if (ticket.Aplicacion == "SGC")
            {
                pak = "PKG_BDU_CLIENT_360_2";
            }
            try
            {
                List<OracleParameter> parameters = new List<OracleParameter>();
                parameters.Add(
                    new OracleParameter(
                        "SCODE",
                        OracleDbType.Varchar2,
                        ticket.Codigo,
                        ParameterDirection.Input
                    )
                );
                parameters.Add(
                    new OracleParameter(
                        "SCODE_JIRA",
                        OracleDbType.Varchar2,
                        ticket.CodigoJIRA,
                        ParameterDirection.Input
                    )
                );
                parameters.Add(
                    new OracleParameter("RC1", OracleDbType.RefCursor, ParameterDirection.Output)
                );
                using (
                    OracleDataReader dr = (OracleDataReader)
                        _connectionBase.ExecuteByStoredProcedure(
                            string.Format("{0}.{1}", pak, "PRC_SET_JIRA"),
                            parameters,
                            ConnectionBase.enuTypeDataBase.OracleVTime
                        )
                )
                {
                    while (dr.Read())
                    {
                        response.Codigo = dr["SCODE"].ToString();
                    }
                }
            }
            catch (Exception)
            {
                response = new Ab_RegContractRes();
            }
            if (response.Codigo != "1")
            {
                response.mensaje = "Hubo error en la actualizacion.";
            }
            else
            {
                response.mensaje = "Actualizacion exitosa";
            }
            return response;
        }
    }
}
