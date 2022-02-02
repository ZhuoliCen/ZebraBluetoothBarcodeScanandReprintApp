using SQLite;

namespace ZebraBluetoothSample.Models
{
    public class Barcode
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Text { get; set; }
    }
}
