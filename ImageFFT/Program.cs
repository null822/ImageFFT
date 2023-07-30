using System.Numerics;
using Accord.Math;
using Accord.Math.Transforms;
using Color = SixLabors.ImageSharp.Color;

namespace ImageFFT;

internal static class Program
{
    private const float CorruptScale = 100f;
    private const float CorruptMin = 40;
    private const float CorruptMax = 60;

    private const bool ParalellFFT = true;

    private static int _width;
    private static int _height;

    private static string _keys = "";
    private static string _name = "";

    private static void Main()
    {
        while (true)
        {
            if (_keys == "")
            {
                Console.WriteLine("Operation(s):");
                Console.WriteLine("  D = Generate data.bytes");
                Console.WriteLine("  O = Generate output.png");
                Console.WriteLine("  A = Generate analysis.png");
                Console.WriteLine("Example:");
                Console.WriteLine("  do,image | da = Generate data/analysis, image = Image Name");
                Console.WriteLine("  da13 | da = Generate data/output, 14 = Image Name");
                var command = Console.ReadLine().ToUpper();
                
                var i = 0;
                foreach (var c in command)
                {
                    if (c == ',')
                    {
                        _name = command[(i+1)..];
                        _keys = command[..i];

                        break;
                    }
                    
                    if (c is not ('D' or 'O' or 'A'))
                    {
                        _name = command[i..];
                        _keys = command[..i];
                        
                        break;
                    }

                    i++;
                }
            }
            
            if (_keys[0] is not ('D' or 'O' or 'A')) throw new Exception("Invalid Operation");
            
            
            ushort operation;

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

            // size of one dimension (r,g,b / mag, pha) * 8 (bits in a double (64-bit float))
            int dimSize;

            #endregion

            switch (_keys[0])
            {
                case 'D': // gen data 
                {
                    operation = 0;

                    if (_keys is [_, 'A', ..])
                    {
                        operation = 3;
                    }
                    
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
                case 'O' or 'A': // gen output/analysis 
                {
                    operation = (ushort)(_keys[0] == 'O' ? 1 : 2);

                    var dataStream = File.Open(@"Images\" + _name + @"\data.bytes", FileMode.Open);


                    var inputBytesArray0 = new byte[8];
                    dataStream.Read(inputBytesArray0, 0, 8);
                    var inputBytes0 = new Span<byte>(inputBytesArray0);

                    _width = BitConverter.ToInt32(inputBytes0[..4]);
                    _height = BitConverter.ToInt32(inputBytes0[4..8]);

                    dimSize = _width * _height * 8;

                    //Console.WriteLine(dimSize);

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

                    // _width = BitConverter.ToInt32(inputBytes0[..4]);
                    // _height = BitConverter.ToInt32(inputBytes0[4..8]);

                    rMag = new double[_width, _height];
                    gMag = new double[_width, _height];
                    bMag = new double[_width, _height];

                    rPha = new double[_width, _height];
                    gPha = new double[_width, _height];
                    bPha = new double[_width, _height];

                    rData = new Complex[_width, _height];
                    gData = new Complex[_width, _height];
                    bData = new Complex[_width, _height];

                    // dimSize = _width * _height * 8;

                    /*
                if (operation == 1) // Corruption 
                {
                    Console.WriteLine("Corrupting");
                    var random = new Random();

                    var corruptMin = (int)(CorruptMin * ((float)dimSize / 100) / 8);
                    var corruptMax = (int)(CorruptMax * ((float)dimSize / 100) / 8);
                    var range = (corruptMax - corruptMin);

                    for (var corrI = 0; corrI < ((float)range / 100) * CorruptScale; corrI++)
                    {
                        var index = random.Next(corruptMin / 8, corruptMax / 8) * 8;

                        for (var j = 0; j < 6; j++)
                        {
                            var currentIndex = index + (j * dimSize);

                            var value = BitConverter.ToDouble(inputBytes[currentIndex..(currentIndex + 8)]);

                            value += random.Next(-64, 64);

                            var newBytes = BitConverter.GetBytes(value);
                            for (var k = 0; k < 8; k++)
                            {
                                inputBytes[currentIndex + k] = newBytes[k];
                            }
                        }
                    }
                }
                */

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
                    throw new Exception("Invalid Operation");
                }
            }

            var data = new FFTData(rMag, gMag, bMag, rPha, gPha, bPha, rData, gData, bData);

            if (operation != 2) data = operation is 0 or 3 ? FFT3Channel2D(data, _width, _height).Result : iFFT3Channel2D(data, _width, _height).Result;
            
            
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

            switch (operation)
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
                        using var stream = new FileStream($"Images/{_name}/data.bytes", FileMode.Create, FileAccess.Write);
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
                            var reCol = Color.FromRgb((byte)data.RCom()[x, y].Real, (byte)data.GCom()[x, y].Real, (byte)data.BCom()[x, y].Real);

                            outputImage[x, y] = reCol;
                        }
                    }

                    Console.WriteLine("Saving");
                    outputImage.SaveAsync($"Images/{_name}/output.png");

                    break;
                }
                case 2 or 3: // gen analysis
                {
                    if (operation == 3)
                    {
                        rMag = data.RMag();
                        gMag = data.GMag();
                        bMag = data.BMag();
                        rPha = data.RPha();
                        gPha = data.GPha();
                        bPha = data.BPha();
                    }
                    
                    var analysisMagImage = new Image<Rgba32>(_width, _height, Color.Black);
                    var analysisPhaImage = new Image<Rgba32>(_width, _height, Color.Black);

                    var magMax = Math.Max(Math.Max(rMag.Max(), gMag.Max()), bMag.Max());
                    
                    var phaOffset = -Math.Min(Math.Min(rPha.Min(), gPha.Min()), bPha.Min()) + 1;
                    var phaMax = Math.Max(Math.Max(rPha.Max(), gPha.Max()), bPha.Max()) + phaOffset;

                    var magLogBase = Math.Pow(magMax, 1d / (_height - 1));
                    var phaLogBase = Math.Pow(phaMax, 1d / (_height - 1));
                    
                    for (var x = 0; x < (float)_width; x++)
                    {
                        for (var y = 0f; y < _height; y++)
                        {
                            #region Magnitude

                            #region Red

                            var rMagValue = rMag[x, (int)y] + 1;
                            var rMagScaledValue = (int)Math.Floor(Math.Log(rMagValue, magLogBase));
                            if (rMagScaledValue < 0) rMagScaledValue = Math.Abs(rMagScaledValue);
                            rMagScaledValue++;

                            if (rMagScaledValue > _height || rMagScaledValue == 0) Console.WriteLine($"RMAG {rMagValue}, {rMagScaledValue}");
                            var color = analysisMagImage[x, _height - rMagScaledValue];

                            analysisMagImage[x, _height - rMagScaledValue] = Color.FromRgb(255, color.G, color.B);

                            #endregion

                            #region Green

                            var gMagValue = gMag[x, (int)y] + 1;
                            var gMagScaledValue = (int)Math.Floor(Math.Log(gMagValue, magLogBase));
                            if (gMagScaledValue < 0) gMagScaledValue = Math.Abs(gMagScaledValue);
                            gMagScaledValue++;

                            if (gMagScaledValue > _height || gMagScaledValue == 0) Console.WriteLine($"GMAG {gMagValue}, {gMagScaledValue}");
                            color = analysisMagImage[x, _height - gMagScaledValue];

                            analysisMagImage[x, _height - gMagScaledValue] = Color.FromRgb(color.R, 255, color.B);

                            #endregion

                            #region Blue

                            var bMagValue = bMag[x, (int)y] + 1;
                            var bMagScaledValue = (int)Math.Floor(Math.Log(bMagValue, magLogBase));
                            if (bMagScaledValue < 0) bMagScaledValue = Math.Abs(bMagScaledValue);
                            bMagScaledValue++;

                            if (bMagScaledValue > _height || bMagScaledValue == 0) Console.WriteLine($"BMAG {bMagValue}, {bMagScaledValue}");
                            color = analysisMagImage[x, _height - bMagScaledValue];

                            analysisMagImage[x, _height - bMagScaledValue] = Color.FromRgb(color.R, color.G, 255);

                            #endregion

                            #endregion

                            #region Phase

                            #region Red

                            var rPhaValue = rPha[x, (int)y] + phaOffset;
                            var rPhaScaledValue = (int)Math.Floor(Math.Log(rPhaValue, phaLogBase));
                            if (rPhaScaledValue < 0) rPhaScaledValue = Math.Abs(rPhaScaledValue);
                            rPhaScaledValue++;

                            if (rPhaScaledValue > _height || rPhaScaledValue == 0) Console.WriteLine($"RPHA {rPhaValue}, {rPhaScaledValue}");

                            color = analysisPhaImage[x, _height - rPhaScaledValue];

                            analysisPhaImage[x, _height - rPhaScaledValue] = Color.FromRgb(255, color.G, color.B);

                            #endregion

                            #region Green

                            var gPhaValue = gPha[x, (int)y] + phaOffset;
                            var gPhaScaledValue = (int)Math.Floor(Math.Log(gPhaValue, phaLogBase));
                            if (gPhaScaledValue < 0) gPhaScaledValue = Math.Abs(gPhaScaledValue);
                            gPhaScaledValue++;

                            if (gPhaScaledValue > _height || gPhaScaledValue == 0) Console.WriteLine($"GPHA {gPhaValue}, {gPhaScaledValue}");
                            color = analysisPhaImage[x, _height - gPhaScaledValue];

                            analysisPhaImage[x, _height - gPhaScaledValue] = Color.FromRgb(color.R, 255, color.B);

                            #endregion

                            #region Blue

                            var bPhaValue = bPha[x, (int)y] + phaOffset;
                            var bPhaScaledValue = (int)Math.Floor(Math.Log(bPhaValue, phaLogBase));
                            if (bPhaScaledValue < 0) bPhaScaledValue = Math.Abs(bPhaScaledValue);
                            bPhaScaledValue++;

                            if (bPhaScaledValue > _height || bPhaScaledValue == 0) Console.WriteLine($"BPHA {bPhaValue}, {bPhaScaledValue}");
                            color = analysisPhaImage[x, _height - bPhaScaledValue];

                            analysisPhaImage[x, _height - bPhaScaledValue] = Color.FromRgb(color.R, color.G, 255);

                            #endregion

                            #endregion
                        }
                    }

                    Console.WriteLine("Saving");

                    analysisMagImage.SaveAsync($"Images/{_name}/analysis_Mag.png");
                    analysisPhaImage.SaveAsync($"Images/{_name}/analysis_Pha.png");

                    break;
                }
            }
           
            if (_keys.Length == 1 || _keys is ['D', 'A'])
            {
                Console.WriteLine("Done");
                return;
            }
            
            _keys = _keys[1..];

        }
    }

    //===============================================================================\\

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
        }
        else
        {
            Console.WriteLine("FFT R");
            FourierTransform2.FFT2(rData, FourierTransform.Direction.Forward);
            Console.WriteLine("FFT G");
            FourierTransform2.FFT2(gData, FourierTransform.Direction.Forward);
            Console.WriteLine("FFT B");
            FourierTransform2.FFT2(bData, FourierTransform.Direction.Forward);
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
        }
        else
        {
            Console.WriteLine("iFFT R");
            FourierTransform2.FFT2(rData, FourierTransform.Direction.Backward);
            Console.WriteLine("iFFT G");
            FourierTransform2.FFT2(gData, FourierTransform.Direction.Backward);
            Console.WriteLine("iFFT B");
            FourierTransform2.FFT2(bData, FourierTransform.Direction.Backward);
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

    #region Helpers

    private static void SpanInsert(Span<byte> target, Span<byte> value, int index)
    {
        for (var i = 0; i < value.Length; i++)
        {
            target[i + index] = value[i];
        }
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