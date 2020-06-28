namespace MusicBoxCompiler.Models
{
    public class Note
    {
        public Instrument Instrument { get; set; }
        public int Number { get; set; }
        public int Pitch { get; set; }
        public string Name { get; set; }
        public double Volume { get; set; }
        public double Length { get; set; }
    }
}
