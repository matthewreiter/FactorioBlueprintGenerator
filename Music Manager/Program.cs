using Music;
using FactoVision.Runtime;
using System.Threading;

InitializeScreen();
LoadSprites();

var previousPosition = 0;
var screenColor = 0;
var rnd = new Random();
//var text = new char[] { 'W', 'h', 'a', 't', ' ', 'i', 's', ' ', 'g', 'o', 'i', 'n', 'g', ' ', 'o', 'n', ' ', 'h', 'e', 'r', 'e', '?', ' ', '(', 'I', ' ', 'm', 'e', 'a', 'n', ',', ' ', 'c', 'o', 'm', 'e', ' ', 'o', 'n', '!', ')' };
var text = new char[] { 'H', 'e', 'l', 'l', 'o', '!' };

Screen.CharacterX = rnd.Next(50);
Screen.CharacterY = rnd.Next(50);

while (true)
{
    var charge = FactoryNetwork.GetValue(Signal.A);
    var currentSong = MusicBox.GetPlayingSongMetadata(MusicBox.SongMetadataField.MetadataAddress);
    var isLow = currentSong == SongMetadataAddresses.Cynthia;
    var requestedSong = MusicBox.RequestedSong;

    if (requestedSong > 0 && requestedSong != currentSong)
    {
        var songAddress = GetSongAddress(requestedSong);

        if (isLow)
        {
            previousPosition = songAddress;
        }
        else
        {
            MusicBox.Position = GetSongAddress(SongMetadataAddresses.Blank);
            Thread.Sleep(2000);
            MusicBox.Position = songAddress;
        }
    }

    if (charge < 30 && !isLow)
    {
        var cynthia = GetSongAddress(SongMetadataAddresses.Cynthia);

        previousPosition = MusicBox.Position;
        MusicBox.Position = cynthia;
    }
    else if (charge > 40 && isLow)
    {
        MusicBox.Position = previousPosition;
        Thread.Sleep(10000);
    }

    Screen.ClearScreen();
    Screen.SetScreenColor((Screen.Color)screenColor);
    Screen.ClearScreen(Screen.PixelValue.Set);
    NumericDisplay.Value = MusicBox.GetPlayingSongMetadata(MusicBox.SongMetadataField.TrackNumber);
    Screen.ClearScreen();

    Screen.SpriteX = rnd.Next(100);
    Screen.SpriteY = rnd.Next(100);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);
    Screen.DrawSprite(2);
    Screen.DrawSprite(3);
    Screen.DrawSprite(4);
    Screen.DrawSprite(1);

    Screen.Print('T');
    Screen.Print('r');
    Screen.Print('a');
    Screen.Print('c');
    Screen.Print('k');
    Screen.Print(' ');
    Screen.Print('#');
    Screen.Print(NumericDisplay.Value);

    //Screen.Print("What is going on here?");
    //Screen.Print(text);

    screenColor = (screenColor + 1) % 8;
}

static int GetSongAddress(int metadataAddress)
{
    MusicBox.SelectSongMetadata(metadataAddress);
    return MusicBox.GetSelectedSongMetadata(MusicBox.SongMetadataField.SongAddress);
}

static void InitializeScreen()
{
    Screen.ClearScreen();
    Screen.SetScreenColor(Screen.Color.White);
    Screen.Font = 1;
    Screen.AutoAdvanceCharacter = true;
    Screen.CharacterX = 70;
    Screen.CharacterY = 56;
    Screen.Print('L');
    Screen.Print('o');
    Screen.Print('a');
    Screen.Print('d');
    Screen.Print('i');
    Screen.Print('n');
    Screen.Print('g');
    Screen.Print('.');
    Screen.Print('.');
    Screen.Print('.');
}

static void LoadSprites()
{
    Screen.WriteSprite(1, 9, 9, new int[]
    {
        0b001111100,
        0b010000010,
        0b100101001,
        0b100101001,
        0b100000001,
        0b101000101,
        0b100111001,
        0b010000010,
        0b001111100
    });

    Screen.WriteSprite(2, 9, 9, new int[]
    {
        0b001111100,
        0b010000010,
        0b100001001,
        0b101100101,
        0b100000101,
        0b101100101,
        0b100001001,
        0b010000010,
        0b001111100
    });

    Screen.WriteSprite(3, 9, 9, new int[]
    {
        0b001111100,
        0b010000010,
        0b100111001,
        0b101000101,
        0b100000001,
        0b100101001,
        0b100101001,
        0b010000010,
        0b001111100
    });

    Screen.WriteSprite(4, 9, 9, new int[]
    {
        0b001111100,
        0b010000010,
        0b100100001,
        0b101001101,
        0b101000001,
        0b101001101,
        0b100100001,
        0b010000010,
        0b001111100
    });
}
