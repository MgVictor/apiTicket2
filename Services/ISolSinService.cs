using apiTicket.Models;
using apiTicket.Models.Reportes;
using OfficeOpenXml;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace apiTicket.Services
{
    public interface ISolSinService
    {
        List<Ab_Adjunto> S3Adjuntar(List<Ab_Adjunto> adjuntos, string type);
        Ab_RegContractRes RegistraTicketJIRAAsync(Ab_Contract request, string type);
    }
}