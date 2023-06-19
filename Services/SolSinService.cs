using apiTicket.Models;
using apiTicket.Models.Reportes;
using apiTicket.Repository;
using apiTicket.Repository.Interfaces;
using apiTicket.Utils;
using apiTicket.Utils.Authentication;

using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;

namespace apiTicket.Services
{
    public class SolSinService : ISolSinService
    {
        private readonly ISolSinRepository _SolSinRepository;

        public SolSinService(ISolSinRepository solSinRepository)
        {
            _SolSinRepository = solSinRepository;
        }

        public List<Ab_Adjunto> S3Adjuntar(List<Ab_Adjunto> adjuntos, string type)
        {
            List<Ab_Adjunto> grabados = new List<Ab_Adjunto>();
            foreach (Ab_Adjunto ar in adjuntos)
            {
                Ab_Adjunto arch = this._SolSinRepository.S3Adjuntar(ar, type);
            }
            return adjuntos;
        }

        public Ab_RegContractRes RegistraTicketJIRAAsync(Ab_Contract request, string type)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logFilePath = Path.Combine(baseDirectory, "logfile.txt");

            try
            {
                string tipo = request.codigo?.Substring(0, 3);
                var tip = string.Empty;
                if (tipo == "SIN")
                {
                    tip = "104";
                }

                List<EstructuraTicket> estructuras = this._SolSinRepository.GetEstructura(tip);
                Ab_TicketDinamico dinamico = new Ab_TicketDinamico();
                dinamico.system = "cliente360";
                dinamico.fields = new List<Field>();
                dinamico.attachments = new List<Ab_Attachment>();
                foreach (EstructuraTicket es in estructuras)
                {
                    if (es.STYPE == "adjuntos")
                    {
                        Ab_Attachment att = new Ab_Attachment();
                        att.id = es.SCODE_JIRA;
                        att.value = new List<string>();
                        if (request.adjunto != null)//SOLO CUANDO TENGA ADJUNTOS //DEV CY -- INI
                        {
                            foreach (Ab_Adjunto arch in request.adjunto)
                            {
                                var ar = this._SolSinRepository.S3Adjuntar(arch, type);
                                att.value.Add(arch.path_gd);
                            }
                            dinamico.attachments.Add(att);
                        } //SOLO CUANDO TENGA ADJUNTOS //DEV CY -- FIN
                    }

                    else
                    {
                        Field campo = AgregarCampo(es, request);
                        if (es.SCODE_JIRA == "customfield_11314")
                        {
                            campo.value = 0;
                        }
                        else
                        {
                            dinamico.fields.Add(campo);
                        }
                        //dinamico.fields.Add(campo);
                    }
                }
                string jsonString = string.Empty;
                jsonString = JsonSerializer.Serialize(dinamico);

                LogToFile(logFilePath, "Operación exitosa");

                Ab_RegContractRes response = this._SolSinRepository.RegistraTicketJIRAAsync(dinamico, type);
                Console.WriteLine(response.Codigo);
                Ab_RegContractRes final = this._SolSinRepository.SetJIRA(new Ticket { Codigo = request.codigo, CodigoJIRA = response.Codigo, Aplicacion = "360" });
                return new Ab_RegContractRes { Codigo = response.Codigo };
            }
            catch (Exception ex)
            {
                LogToFile(logFilePath, $"Error: {ex.Message}\nStack Trace: {ex.StackTrace}");

                // Puedes lanzar la excepci�n nuevamente para que se maneje en el nivel superior si es necesario
                throw;
            }            
        }
        private void LogToFile(string logFilePath, string message)
        {
            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            }
        }
        public Field AgregarCampo(EstructuraTicket est, Ab_Contract ticket)
        {
            object value = null;

            if (est.STYPE == "summary" || est.STYPE == "string" || est.STYPE == "description")
            {
                value = ticket.GetType().GetProperty(est.SDESCRIPT).GetValue(ticket).ToString();
            }
            if (est.STYPE == "datetime")
            {
                var fecha = ticket.GetType().GetProperty(est.SDESCRIPT).GetValue(ticket);
                string conver = DateTime.Parse(fecha.ToString()).ToString("yyyy-MM-dd");
                value = conver + "T05:00:00.000+0000";
            }
            if (est.STYPE == "issuetype" || est.STYPE == "option")
            {
                var valor = ticket.GetType().GetProperty(est.SDESCRIPT).GetValue(ticket).ToString();
                value = new ID { id = valor };
            }
            if (est.STYPE == "project")
            {
                var valor = ticket.GetType().GetProperty(est.SDESCRIPT).GetValue(ticket).ToString();
                value = new Key { key = valor };
            }
            if (est.STYPE == "user" || est.STYPE == "reporter")
            {
                var valor = ticket.GetType().GetProperty(est.SDESCRIPT).GetValue(ticket).ToString();
                value = new Name { name = valor };
            }
            if (est.STYPE == "array")
            {
                var valor = ticket.GetType().GetProperty(est.SDESCRIPT).GetValue(ticket).ToString();
                value = new List<ID> { new ID { id = valor } };
            }

            if (est.STYPE == "option-with-child")
            {
                var valor = ticket.GetType().GetProperty(est.SDESCRIPT).GetValue(ticket).ToString();
                value = new Child { id = ticket.ramo, child = new ID { id = ticket.producto } };
            }

            Field field = new Field { id = est.SCODE_JIRA, value = value };
            return field;
        }
    }
}