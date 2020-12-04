using Music;
using SeeSharp.Runtime;

namespace MusicManager
{
    public class Program
    {
        public static void Main()
        {
            var previousPosition = 0;

            while (true)
            {
                var charge = FactoryNetwork.GetValue(Signal.A);
                var isLow = MusicBox.GetPlayingSongMetadata(MusicBox.SongMetadataField.MetadataAddress) == SongMetadataAddresses.Cynthia;

                if (charge < 50 && !isLow)
                {
                    MusicBox.SelectSongMetadata(SongMetadataAddresses.Cynthia); // Do this each time in case songs are redeployed while the program is running
                    var cynthia = MusicBox.GetSelectedSongMetadata(MusicBox.SongMetadataField.SongAddress);

                    previousPosition = MusicBox.Position;
                    MusicBox.Position = cynthia;
                }
                else if (charge > 55 && isLow)
                {
                    MusicBox.Position = previousPosition;
                }

                NumericDisplay.Value = MusicBox.GetPlayingSongMetadata(MusicBox.SongMetadataField.TrackNumber);
            }
        }
    }
}
