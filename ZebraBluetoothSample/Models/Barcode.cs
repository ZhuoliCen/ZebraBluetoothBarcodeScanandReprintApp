using SQLite;

namespace ZebraBluetoothSample.Models
{
    public class Barcode
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Text { get; set; }
        public string Code { get; set; }
        public string ItemNumber { get; set; }
        public string NetWeight { get; set; }

    }
}
