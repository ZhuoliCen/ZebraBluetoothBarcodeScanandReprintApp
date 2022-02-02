using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraBluetoothSample.Models;

namespace ZebraBluetoothSample.Dependencies
{
    public interface IBarcodeService
    {
        Task AddBarcode(string text);
        Task<IEnumerable<Barcode>> GetBarcode();
        Task<Barcode> GetBarcode(int id);
        Task RemoveBarcode(int id);
    }
}
