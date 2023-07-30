using System.Numerics;
using Accord.Math;
using Accord.Math.Transforms;
using Color = SixLabors.ImageSharp.Color;

namespace ImageFFT;

internal static class Program
{
    /* All Corruption params (except CorruptPotential) are in percent */
    private const float CorruptAmount = 0;
    private const int CorruptPotential = 64;
    private const float CorruptMinFreq = 40;
    private const float CorruptMaxFreq = 60;

    private const bool ParalellFFT = true;

    private static int _width;
    private static int _height;

    private static double _magLogBase;
    private static double _phaLogBase;

    private static string _name = "";
    
    private static ushort _source;
    private static ushort _operation;

    private static void Main()
    {
        Console.Clear();
        while (true)
        {

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Operation(s):");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  I = Load Image");
            Console.WriteLine("  D = Load Data");
            Console.WriteLine("  D = Generate Data");
            Console.WriteLine("  O = Generate Output");
            Console.WriteLine("  A = Generate Analysis");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Example:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  do,image | \"do\" =  Load Data, Generate Output,   \"image\" = Image Name");
            Console.WriteLine("  ia13     | \"ia\" = Load Image, Generate Analysis, \"13\" =    Image Name");
            Console.ForegroundColor = ConsoleColor.White;
            var command = Console.ReadLine() ?? throw new Exception("Invalid Command");

            _name = command[2..];
            _source = char.ToUpper(command[0]) switch
            {
                'I' => 0,
                'D' => 1,
                _ => throw new Exception("Invalid Command")
            };

            _operation = char.ToUpper(command[1]) switch
            {
                'D' => 0,
                'O' => 1,
                'A' => 2,
                _ => throw new Exception("Invalid Command")
            };

            #region Variable Creation

            double[,] rMag;
            double[,] gMag;
            double[,] bMag;

            double[,] rPha;
            double[,] gPha;
            double[,] bPha;

            Complex[,] rData;
            Complex[,] gData;
            Complex[,] bData;

            // size of one dimension (r,g,b / mag,pha) * 8 (bits in a double)
            int dimSize;

            #endregion

            switch (_source)
            {
                case 0: // load image
                {
                    var imageStream = File.Open(@"Images\" + _name + ".png", FileMode.Open);
                    var imageFile = Image.Load(imageStream);
                    imageStream.Close();

                    var image = imageFile.CloneAs<Rgba32>();

                    _width = image.Width;
                    _height = image.Height;

                    rMag = new double[_width, _height];
                    gMag = new double[_width, _height];
                    bMag = new double[_width, _height];

                    rPha = new double[_width, _height];
                    gPha = new double[_width, _height];
                    bPha = new double[_width, _height];

                    rData = new Complex[_width, _height];
                    gData = new Complex[_width, _height];
                    bData = new Complex[_width, _height];

                    dimSize = _width * _height * 8;

                    for (var x = 0; x < _width; x++)
                    {
                        for (var y = 0; y < _height; y++)
                        {
                            rData[x, y] = new Complex(image[x, y].R, 0);
                            gData[x, y] = new Complex(image[x, y].G, 0);
                            bData[x, y] = new Complex(image[x, y].B, 0);
                        }
                    }

                    break;
                }
                case 1: // load data.bytes
                {

                    var dataStream = File.Open(@"Images\" + _name + @"\data.bytes", FileMode.Open);

                    var inputBytesArray0 = new byte[8];
                    dataStream.Read(inputBytesArray0, 0, 8);
                    var inputBytes0 = new Span<byte>(inputBytesArray0);

                    _width = BitConverter.ToInt32(inputBytes0[..4]);
                    _height = BitConverter.ToInt32(inputBytes0[4..8]);

                    dimSize = _width * _height * 8;

                    var inputBytesArray1 = new byte[dimSize];
                    var inputBytesArray2 = new byte[dimSize];
                    var inputBytesArray3 = new byte[dimSize];
                    var inputBytesArray4 = new byte[dimSize];
                    var inputBytesArray5 = new byte[dimSize];
                    var inputBytesArray6 = new byte[dimSize];

                    dataStream.Read(inputBytesArray1, 0, dimSize);
                    dataStream.Read(inputBytesArray2, 0, dimSize);
                    dataStream.Read(inputBytesArray3, 0, dimSize);
                    dataStream.Read(inputBytesArray4, 0, dimSize);
                    dataStream.Read(inputBytesArray5, 0, dimSize);
                    dataStream.Read(inputBytesArray6, 0, dimSize);
                    dataStream.Close();

                    var inputBytes1 = new Span<byte>(inputBytesArray1);
                    var inputBytes2 = new Span<byte>(inputBytesArray2);
                    var inputBytes3 = new Span<byte>(inputBytesArray3);
                    var inputBytes4 = new Span<byte>(inputBytesArray4);
                    var inputBytes5 = new Span<byte>(inputBytesArray5);
                    var inputBytes6 = new Span<byte>(inputBytesArray6);

                    rMag = new double[_width, _height];
                    gMag = new double[_width, _height];
                    bMag = new double[_width, _height];

                    rPha = new double[_width, _height];
                    gPha = new double[_width, _height];
                    bPha = new double[_width, _height];

                    rData = new Complex[_width, _height];
                    gData = new Complex[_width, _height];
                    bData = new Complex[_width, _height];

                    if (_operation == 1) // Corruption 
                    {
                        Console.WriteLine("Corrupting");
                        var random = new Random();

                        var corruptMin = (int)(CorruptMinFreq * ((float)dimSize / 100) / 8);
                        var corruptMax = (int)(CorruptMaxFreq * ((float)dimSize / 100) / 8);
                        var range = (corruptMax - corruptMin);

                        for (var corrI = 0; corrI < ((float)range / 100) * CorruptAmount; corrI++)
                        {
                            var index = random.Next(corruptMin / 8, corruptMax / 8) * 8;

                            for (var j = 0; j < 6; j++)
                            {
                                var dim = random.Next(0, 5);

                                var value = dim switch
                                {
                                    0 => BitConverter.ToDouble(inputBytes1[index..(index + 8)]),
                                    1 => BitConverter.ToDouble(inputBytes2[index..(index + 8)]),
                                    2 => BitConverter.ToDouble(inputBytes3[index..(index + 8)]),
                                    3 => BitConverter.ToDouble(inputBytes4[index..(index + 8)]),
                                    4 => BitConverter.ToDouble(inputBytes5[index..(index + 8)]),
                                    5 => BitConverter.ToDouble(inputBytes6[index..(index + 8)]),
                                    _ => 0
                                };

                                value += random.Next(-CorruptPotential, CorruptPotential);

                                var newBytes = BitConverter.GetBytes(value);

                                switch (dim)
                                {
                                    case 0:
                                    {
                                        for (var k = 0; k < 8; k++)
                                        {
                                            inputBytes1[index + k] = newBytes[k];
                                        }

                                        break;
                                    }
                                    case 1:
                                    {
                                        for (var k = 0; k < 8; k++)
                                        {
                                            inputBytes2[index + k] = newBytes[k];
                                        }

                                        break;
                                    }
                                    case 2:
                                    {
                                        for (var k = 0; k < 8; k++)
                                        {
                                            inputBytes3[index + k] = newBytes[k];
                                        }

                                        break;
                                    }
                                    case 3:
                                    {
                                        for (var k = 0; k < 8; k++)
                                        {
                                            inputBytes4[index + k] = newBytes[k];
                                        }

                                        break;
                                    }
                                    case 4:
                                    {
                                        for (var k = 0; k < 8; k++)
                                        {
                                            inputBytes5[index + k] = newBytes[k];
                                        }

                                        break;
                                    }
                                    case 5:
                                    {
                                        for (var k = 0; k < 8; k++)
                                        {
                                            inputBytes6[index + k] = newBytes[k];
                                        }

                                        break;
                                    }
                                }

                            }
                        }
                    }

                    var i1 = 0;
                    for (var x = 0; x < _width; x++)
                    {
                        for (var y = 0; y < _height; y++)
                        {
                            rMag[x, y] = BitConverter.ToDouble(inputBytes1[i1..(i1 + 8)]);
                            gMag[x, y] = BitConverter.ToDouble(inputBytes2[i1..(i1 + 8)]);
                            bMag[x, y] = BitConverter.ToDouble(inputBytes3[i1..(i1 + 8)]);
                            rPha[x, y] = BitConverter.ToDouble(inputBytes4[i1..(i1 + 8)]);
                            gPha[x, y] = BitConverter.ToDouble(inputBytes5[i1..(i1 + 8)]);
                            bPha[x, y] = BitConverter.ToDouble(inputBytes6[i1..(i1 + 8)]);

                            i1 += 8;
                        }
                    }

                    break;
                }
                default:
                {
                    throw new Exception("Invalid Command");
                }
            }

            var data = new FFTData(rMag, gMag, bMag, rPha, gPha, bPha, rData, gData, bData);

            data = _source == 0
                ? FFT3Channel2D(data, _width, _height).Result
                : iFFT3Channel2D(data, _width, _height).Result;


            var dir = @"Images\" + _name;
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(@"Images\" + _name);
                }
                catch
                {
                }
            }

            switch (_operation)
            {
                case 0: // gen data 
                {
                    //Console.WriteLine((long)dimSize * 3 * 2 + 8);

                    var bytes0 = new Span<byte>(new byte[8]);
                    var bytes1 = new Span<byte>(new byte[dimSize]);
                    var bytes2 = new Span<byte>(new byte[dimSize]);
                    var bytes3 = new Span<byte>(new byte[dimSize]);
                    var bytes4 = new Span<byte>(new byte[dimSize]);
                    var bytes5 = new Span<byte>(new byte[dimSize]);
                    var bytes6 = new Span<byte>(new byte[dimSize]);

                    SpanInsert(bytes0, BitConverter.GetBytes(_width), 0);
                    SpanInsert(bytes0, BitConverter.GetBytes(_height), 4);

                    var i = 0;
                    for (var x = 0; x < _width; x++)
                    {
                        for (var y = 0; y < _height; y++)
                        {
                            SpanInsert(bytes1, BitConverter.GetBytes(data.RMag()[x, y]), i);
                            SpanInsert(bytes2, BitConverter.GetBytes(data.GMag()[x, y]), i);
                            SpanInsert(bytes3, BitConverter.GetBytes(data.BMag()[x, y]), i);
                            SpanInsert(bytes4, BitConverter.GetBytes(data.RPha()[x, y]), i);
                            SpanInsert(bytes5, BitConverter.GetBytes(data.GPha()[x, y]), i);
                            SpanInsert(bytes6, BitConverter.GetBytes(data.BPha()[x, y]), i);

                            i += 8;
                        }
                    }

                    Console.WriteLine("Saving");
                    try
                    {
                        using var stream = new FileStream($"Images/{_name}/data.bytes", FileMode.Create,
                            FileAccess.Write);
                        stream.Write(bytes0);
                        stream.Write(bytes1);
                        stream.Write(bytes2);
                        stream.Write(bytes3);
                        stream.Write(bytes4);
                        stream.Write(bytes5);
                        stream.Write(bytes6);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    break;
                }
                case 1: // gen output 
                {
                    var outputImage = new Image<Rgba32>(_width, _height, Color.Black);

                    for (var x = 0; x < _width; x++)
                    {
                        for (var y = 0; y < _height; y++)
                        {
                            var reCol = Color.FromRgb((byte)data.RCom()[x, y].Real, (byte)data.GCom()[x, y].Real,
                                (byte)data.BCom()[x, y].Real);

                            outputImage[x, y] = reCol;
                        }
                    }

                    Console.WriteLine("Saving");
                    outputImage.SaveAsync($"Images/{_name}/output.png");

                    break;
                }
                case 2: // gen analysis
                {
                    if (_source == 0) // data from image, get fromm FFT results
                    {
                        rMag = data.RMag();
                        gMag = data.GMag();
                        bMag = data.BMag();
                        rPha = data.RPha();
                        gPha = data.GPha();
                        bPha = data.BPha();
                    }


                    var magMax = Math.Max(Math.Max(rMag.Max(), gMag.Max()), bMag.Max());

                    var phaOffset = -Math.Min(Math.Min(rPha.Min(), gPha.Min()), bPha.Min()) + 1;
                    var phaMax = Math.Max(Math.Max(rPha.Max(), gPha.Max()), bPha.Max()) + phaOffset;

                    _magLogBase = Math.Pow(magMax, 1d / (_height - 1));
                    _phaLogBase = Math.Pow(phaMax, 1d / (_height - 1));

                    var analysisMagImage = new Image<Rgb48>(_width, _height, new Rgb48(0, 0, 0));
                    var analysisPhaImage = new Image<Rgb48>(_width, _height, new Rgb48(0, 0, 0));

                    Console.ForegroundColor = ConsoleColor.Red;
                    for (var x = 0; x < (float)_width; x++)
                    {
                        for (var y = 0f; y < _height; y++)
                        {
                            #region Magnitude

                            var scaled = MagHeightCalculate(rMag[x, (int)y] + 1);
                            var color = analysisMagImage[x, _height - scaled];
                            analysisMagImage[x, _height - scaled] = new Rgb48(65535, color.G, color.B);

                            scaled = MagHeightCalculate(gMag[x, (int)y] + 1);
                            color = analysisMagImage[x, _height - scaled];
                            analysisMagImage[x, _height - scaled] = new Rgb48(color.R, 65535, color.B);

                            scaled = MagHeightCalculate(bMag[x, (int)y] + 1);
                            color = analysisMagImage[x, _height - scaled];
                            analysisMagImage[x, _height - scaled] = new Rgb48(color.R, color.G, 65535);

                            #endregion

                            #region Phase

                            scaled = PhaHeightCalculate(rPha[x, (int)y] + phaOffset);
                            color = analysisPhaImage[x, _height - scaled];
                            analysisPhaImage[x, _height - scaled] = new Rgb48(65535, color.G, color.B);

                            scaled = PhaHeightCalculate(gPha[x, (int)y] + phaOffset);
                            color = analysisPhaImage[x, _height - scaled];
                            analysisPhaImage[x, _height - scaled] = new Rgb48(color.R, 65535, color.B);

                            scaled = PhaHeightCalculate(bPha[x, (int)y] + phaOffset);
                            color = analysisPhaImage[x, _height - scaled];
                            analysisPhaImage[x, _height - scaled] = new Rgb48(color.R, color.G, 65535);

                            #endregion
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Saving");

                    analysisMagImage.SaveAsync($"Images/{_name}/analysis_Mag.png");
                    analysisPhaImage.SaveAsync($"Images/{_name}/analysis_Pha.png");

                    break;
                }
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Done");

            Console.ReadKey();
            
            Console.Clear();

        }

    }

    //===============================================================================\\

    #region Helpers

    private static int MagHeightCalculate(double input)
    {
        var scaledValue = (int)Math.Floor(Math.Log(input, _magLogBase));
        if (scaledValue == int.MinValue) scaledValue = 0;
        if (scaledValue < 0)
        {
            Console.Error.WriteLine("LOW Scaled Mag");
            scaledValue = 0;
        }
        scaledValue++;

        if (scaledValue > _height) Console.Error.WriteLine($"HIGH Scaled Mag. input: {input}, scaled: {scaledValue}");
        
        return scaledValue;
    }
    
    private static int PhaHeightCalculate(double input)
    {
        var scaledValue = (int)Math.Floor(Math.Log(input, _phaLogBase));
        if (scaledValue == int.MinValue) scaledValue = 0;
        if (scaledValue < 0)
        {
            Console.Error.WriteLine("LOW Scaled Pha");
            scaledValue = 0;
        }
        scaledValue++;

        if (scaledValue > _height) Console.Error.WriteLine($"HIGH Scaled Pha. input: {input}, scaled: {scaledValue}");

        return scaledValue;
    }
    
    private static void SpanInsert(Span<byte> target, Span<byte> value, int index)
    {
        for (var i = 0; i < value.Length; i++)
        {
            target[i + index] = value[i];
        }
    }

    #endregion
    
    #region FFTs
        
    private static async Task<FFTData> FFT3Channel2D(FFTData data, int width, int height)
    {
            
        var rData = new Complex[width][];
        var gData = new Complex[width][];
        var bData = new Complex[width][];
        

        for (var x = 0; x < width; x++)
        {
            rData[x] = new Complex[height];
            gData[x] = new Complex[height];
            bData[x] = new Complex[height];
            
            for (var y = 0; y < height; y++)
            {
                rData[x][y] = data.RCom()[x, y];
                gData[x][y] = data.GCom()[x, y];
                bData[x][y] = data.BCom()[x, y];
            }
        }

        if (ParalellFFT)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("FFTs");

            var rTask = new Task<Complex[][]>(() =>
            {
                FourierTransform2.FFT2(rData, FourierTransform.Direction.Forward);
                return rData;
            });

            var gTask = new Task<Complex[][]>(() =>
            {
                FourierTransform2.FFT2(gData, FourierTransform.Direction.Forward);
                return rData;
            });

            var bTask = new Task<Complex[][]>(() =>
            {
                FourierTransform2.FFT2(bData, FourierTransform.Direction.Forward);
                return rData;
            });

            rTask.Start();
            gTask.Start();
            bTask.Start();

            await Task.WhenAll(new Task[] { rTask, gTask, bTask });

            Console.WriteLine("FFTs Complete");
            Console.ForegroundColor = ConsoleColor.White;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("FFT R");
            FourierTransform2.FFT2(rData, FourierTransform.Direction.Forward);
            Console.WriteLine("FFT G");
            FourierTransform2.FFT2(gData, FourierTransform.Direction.Forward);
            Console.WriteLine("FFT B");
            FourierTransform2.FFT2(bData, FourierTransform.Direction.Forward);
            Console.ForegroundColor = ConsoleColor.White;
        }




        var rMagOut = new double[width, height];
        var gMagOut = new double[width, height];
        var bMagOut = new double[width, height];
            
        var rPhaOut = new double[width, height];
        var gPhaOut = new double[width, height];
        var bPhaOut = new double[width, height];
            
        var rComOut = new Complex[width, height];
        var gComOut = new Complex[width, height];
        var bComOut = new Complex[width, height];
        
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                rMagOut[x, y] = rData[x][y].Magnitude;
                gMagOut[x, y] = gData[x][y].Magnitude;
                bMagOut[x, y] = bData[x][y].Magnitude;

                rPhaOut[x, y] = rData[x][y].Phase;
                gPhaOut[x, y] = gData[x][y].Phase;
                bPhaOut[x, y] = bData[x][y].Phase;

                rComOut[x, y] = rData[x][y];
                gComOut[x, y] = gData[x][y];
                bComOut[x, y] = bData[x][y];
            }
        }

        var results = new FFTData(
            rMagOut, gMagOut, bMagOut,
            rPhaOut, gPhaOut, bPhaOut,
            rComOut, gComOut, bComOut
        );

        return results;
    }

    private static async Task<FFTData> iFFT3Channel2D(FFTData data, int width, int height)
    {

        var rData = new Complex[width][];
        var gData = new Complex[width][];
        var bData = new Complex[width][];

        for (var x = 0; x < width; x++)
        {
            rData[x] = new Complex[height];
            gData[x] = new Complex[height];
            bData[x] = new Complex[height];

            for (var y = 0; y < height; y++)
            {

                rData[x][y] = Complex.FromPolarCoordinates(data.RMag()[x, y], data.RPha()[x, y]);
                gData[x][y] = Complex.FromPolarCoordinates(data.GMag()[x, y], data.GPha()[x, y]);
                bData[x][y] = Complex.FromPolarCoordinates(data.BMag()[x, y], data.BPha()[x, y]);
            }
        }
        
        if (ParalellFFT)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("iFFTs");

            var rTask = new Task<Complex[][]>(() =>
            {
                FourierTransform2.FFT2(rData, FourierTransform.Direction.Backward);
                return rData;
            });

            var gTask = new Task<Complex[][]>(() =>
            {
                FourierTransform2.FFT2(gData, FourierTransform.Direction.Backward);
                return rData;
            });

            var bTask = new Task<Complex[][]>(() =>
            {
                FourierTransform2.FFT2(bData, FourierTransform.Direction.Backward);
                return rData;
            });

            rTask.Start();
            gTask.Start();
            bTask.Start();

            await Task.WhenAll(new Task[] { rTask, gTask, bTask });

            Console.WriteLine("iFFTs Complete");
            
            Console.ForegroundColor = ConsoleColor.White;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("iFFT R");
            FourierTransform2.FFT2(rData, FourierTransform.Direction.Backward);
            Console.WriteLine("iFFT G");
            FourierTransform2.FFT2(gData, FourierTransform.Direction.Backward);
            Console.WriteLine("iFFT B");
            FourierTransform2.FFT2(bData, FourierTransform.Direction.Backward);
            Console.ForegroundColor = ConsoleColor.White;
        }
        

        var rMagOut = new double[width, height];
        var gMagOut = new double[width, height];
        var bMagOut = new double[width, height];
        
        var rPhaOut = new double[width, height];
        var gPhaOut = new double[width, height];
        var bPhaOut = new double[width, height];

        var rComOut = new Complex[width, height];
        var gComOut = new Complex[width, height];
        var bComOut = new Complex[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                /*
                Console.WriteLine(dataR[x][y]);
                Console.WriteLine(dataG[x][y]);
                Console.WriteLine(dataB[x][y]);
                */
                var rMag = Math.Floor(rData[x][y].Magnitude * 256);
                var gMag = Math.Floor(gData[x][y].Magnitude * 256);
                var bMag = Math.Floor(bData[x][y].Magnitude * 256);

                var rPha = Math.Floor(rData[x][y].Phase * 256);
                var gPha = Math.Floor(gData[x][y].Phase * 256);
                var bPha = Math.Floor(bData[x][y].Phase * 256);

                rMagOut[x, y] = rMag * 256;
                gMagOut[x, y] = gMag * 256;
                bMagOut[x, y] = bMag * 256;

                rPhaOut[x, y] = rPha * 256;
                gPhaOut[x, y] = gPha * 256;
                bPhaOut[x, y] = bPha * 256;
                
                rComOut[x, y] = rData[x][y];
                gComOut[x, y] = gData[x][y];
                bComOut[x, y] = bData[x][y];

            }
        }
        
        var results = new FFTData(
            rMagOut, gMagOut, bMagOut,
            rPhaOut, gPhaOut, bPhaOut,
            rComOut, gComOut, bComOut
        );
        
        return results;
    }
    #endregion

    private struct FFTData
    {
        private readonly Complex[,] _empty = new Complex[_width, _height];
        
        private readonly double[,] _rMag;
        private readonly double[,] _gMag;
        private readonly double[,] _bMag;
            
        private readonly double[,] _rPha;
        private readonly double[,] _gPha;
        private readonly double[,] _bPha;

        private readonly Complex[,]? _rCom;
        private readonly Complex[,]? _gCom;
        private readonly Complex[,]? _bCom;

        public FFTData(
            double[,] rMag, double[,] gMag, double[,] bMag,
            double[,] rPha, double[,] gPha, double[,] bPha,
            Complex[,]? rCom = null, Complex[,]? gCom = null, Complex[,]? bCom = null
                
        )
        {
            _rMag = rMag;
            _gMag = gMag;
            _bMag = bMag;

            _rPha = rPha;
            _gPha = gPha;
            _bPha = bPha;

            _rCom = rCom;
            _gCom = gCom;
            _bCom = bCom;
                
        }
            
        public double[,] RMag()
        {
            return _rMag;
        }
        public double[,] GMag()
        {
            return _gMag;
        }
        public double[,] BMag()
        {
            return _bMag;
        }
        public double[,] RPha()
        {
            return _rPha;
        }
        public double[,] GPha()
        {
            return _gPha;
        }
        public double[,] BPha()
        {
            return _bPha;
        }
        public Complex[,] RCom()
        {
            return _rCom ?? _empty;
        }
        public Complex[,] GCom()
        {
            return _gCom ?? _empty;
        }
        public Complex[,] BCom()
        {
            return _bCom ?? _empty;
        }
        
    }

}