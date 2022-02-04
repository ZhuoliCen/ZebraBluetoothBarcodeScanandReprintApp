using ZebraBluetoothSample.Dependencies;
using ZebraBluetoothSample.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

[assembly: Dependency(typeof(BarcodeService))]
namespace ZebraBluetoothSample.Dependencies
{
    public class BarcodeService : IBarcodeService
    {
        SQLiteAsyncConnection db;
        async Task Init()
        {
            if (db != null)
                return;

            // Get an absolute path to the database file
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "MyData.db");

            db = new SQLiteAsyncConnection(databasePath);

            await db.CreateTableAsync<Barcode>();
        }

        public async Task AddBarcode(string text)
        {
            await Init();
            var code = text.Substring(0, 4);
            var itemNumber = text.Substring(4, 8);
            var netWeight = text.Substring(12, text.Length-12);

            var barcode = new Barcode
            {
                Text = text,
                Code = code,
                ItemNumber = itemNumber,
                NetWeight = netWeight
            };

            var id = await db.InsertAsync(barcode);
        }

        public async Task RemoveBarcode(int id)
        {

            await Init();

            await db.DeleteAsync<Barcode>(id);
        }

        public async Task<IEnumerable<Barcode>> GetBarcode()
        {
            await Init();

            var barcode = await db.Table<Barcode>().ToListAsync();
            return barcode;
        }

        public async Task<Barcode> GetBarcode(int id)
        {
            await Init();

            var barcode = await db.Table<Barcode>()
                .FirstOrDefaultAsync(c => c.Id == id);

            return barcode;
        }
    }
}
