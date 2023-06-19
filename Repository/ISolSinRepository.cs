using apiTicket.Models;
using apiTicket.Models.Reportes;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace apiTicket.Repository.Interfaces
{
    public interface ISolSinRepository
    {
        Ab_Adjunto S3Adjuntar(Ab_Adjunto ar, string type, string customfield = "customfield_12801");
        List<EstructuraTicket> GetEstructura(string Tipo);
        Ab_RegContractRes RegistraTicketJIRAAsync(Ab_TicketDinamico request, string type);
        Ab_RegContractRes SetJIRA(Ticket ticket);
        // Task<Ab_RegContractRes> RegistraTicketJIRAAsync(string jsonString, string type);
    }
}
