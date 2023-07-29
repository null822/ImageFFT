using System.Numerics;
using Accord.Math;
using Accord.Math.Transforms;
using Color = SixLabors.ImageSharp.Color;

namespace ImageFFT;

internal static class Program
{
    private const bool Debug = true;
    private const float CorruptScale = 100f;
    private const float CorruptMin = 40;
    private const float CorruptMax = 60;

    private static int _width;
    private static int _height;
    
    private static void Main()
    {
        
        Console.WriteLine("Operation:");
        Console.WriteLine("  D = Generate data.bytes");
        Console.WriteLine("  O = Generate output.png");
        Console.WriteLine("  A = Generate analysis.png");
        var key = Console.ReadKey().Key;
        Console.WriteLine("                                     ");

        if (key is not (ConsoleKey.D or ConsoleKey.O or ConsoleKey.A))
            throw new Exception("Invalid Operation");
        
        
        Console.WriteLine("Image Name (excluding extension): ");
        var name = Console.ReadLine();
        
        ushort operation = 0;

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

        switch (key)
        {
            case ConsoleKey.D: // gen data 
            {
                operation = 0;
                
                var imageStream = File.Open(@"Images\" + name + ".png", FileMode.Open);
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
            case ConsoleKey.O or ConsoleKey.A: // gen output/analysis 
            {
                operation = (ushort)(key == ConsoleKey.O ? 1 : 2);
            
                var dataStream = File.Open(@"Images\" + name + @"\data.bytes", FileMode.Open);

                var inputBytes = new Span<byte>(new byte[dataStream.Length]);

                dataStream.Read(inputBytes);
                dataStream.Close();
            
                _width = BitConverter.ToInt32(inputBytes[..4]);
                _height = BitConverter.ToInt32(inputBytes[4..8]);
            
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

                var i1 = 0;
                for (var x = 0; x < _width; x++)
                {
                    for (var y = 0; y < _height; y++)
                    {
                        var rMagIndex = i1 + (dimSize * 0) + 8;
                        var gMagIndex = i1 + (dimSize * 1) + 8;
                        var bMagIndex = i1 + (dimSize * 2) + 8;
                        var rPhaIndex = i1 + (dimSize * 3) + 8;
                        var gPhaIndex = i1 + (dimSize * 4) + 8;
                        var bPhaIndex = i1 + (dimSize * 5) + 8;
                    
                    
                        rMag[x, y] = BitConverter.ToDouble(inputBytes[rMagIndex..(rMagIndex + 8)]);
                        gMag[x, y] = BitConverter.ToDouble(inputBytes[gMagIndex..(gMagIndex + 8)]);
                        bMag[x, y] = BitConverter.ToDouble(inputBytes[bMagIndex..(bMagIndex + 8)]);
                        rPha[x, y] = BitConverter.ToDouble(inputBytes[rPhaIndex..(rPhaIndex + 8)]);
                        gPha[x, y] = BitConverter.ToDouble(inputBytes[gPhaIndex..(gPhaIndex + 8)]);
                        bPha[x, y] = BitConverter.ToDouble(inputBytes[bPhaIndex..(bPhaIndex + 8)]);
                    
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

        if (operation != 2)
            data = operation == 0 ? FFT3Channel2D(data, _width, _height).Result : iFFT3Channel2D(data, _width, _height).Result;
        
        
        var dir = @"Images\" + name;
        if (!Directory.Exists(dir))
        {
            try
            {
                Directory.CreateDirectory(@"Images\" + name);
            } catch { }
        }

        switch (operation)
        {
            case 0: // gen data 
            {
                var bytes = new byte[(dimSize * 3 * 2) + 8];
                
                Array.Copy(BitConverter.GetBytes(_width), 0, bytes, 0, 4);
                Array.Copy(BitConverter.GetBytes(_height), 0, bytes, 4, 4);
                
                var i = 0;
                for (var x = 0; x < _width; x++)
                {
                    for (var y = 0; y < _height; y++)
                    {
                        Array.Copy(BitConverter.GetBytes(
                                data.RMag()[x, y]), 0, bytes,
                            i + (dimSize * 0) + 8, 8);
                        Array.Copy(BitConverter.GetBytes(
                                data.GMag()[x, y]), 0, bytes,
                            i + (dimSize * 1) + 8, 8);
                        Array.Copy(BitConverter.GetBytes(
                                data.BMag()[x, y]), 0, bytes,
                            i + (dimSize * 2) + 8, 8);
                        Array.Copy(BitConverter.GetBytes(
                                data.RPha()[x, y]), 0, bytes,
                            i + (dimSize * 3) + 8, 8);
                        Array.Copy(BitConverter.GetBytes(
                                data.GPha()[x, y]), 0, bytes,
                            i + (dimSize * 4) + 8, 8);
                        Array.Copy(BitConverter.GetBytes(
                                data.BPha()[x, y]), 0, bytes,
                            i + (dimSize * 5) + 8, 8);
                        
                        i += 8;
                    }
                }
                
                try
                {
                    using var stream = new FileStream($"Images/{name}/data.bytes", FileMode.Create, FileAccess.Write);
                    stream.Write(bytes, 0, bytes.Length);
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
                        var reCol = Color.FromRgb(
                            (byte)data.RCom()[x, y].Real,
                            (byte)data.GCom()[x, y].Real,
                            (byte)data.BCom()[x, y].Real);
                
                        outputImage[x, y] = reCol;
                    }
                }
                
                outputImage.SaveAsync($"Images/{name}/output.png");

                break;
            }
            case 2: // gen analysis
            {
                var analysisMagImage = new Image<Rgba32>(_width, _height, Color.Black);
                var analysisPhaImage = new Image<Rgba32>(_width, _height, Color.Black);


                var magMax = Math.Max(
                    Math.Max(
                        rMag.Max(),
                        gMag.Max()
                    ), bMag.Max()
                );
                
                var phaOffset = -Math.Min(
                    Math.Min(
                        rPha.Min(),
                        gPha.Min()
                    ), bPha.Min()
                ) + 1;
                
                var phaMax = Math.Max(
                    Math.Max(
                        rPha.Max(),
                        gPha.Max()
                    ), bPha.Max()
                ) + phaOffset;
                
                
                var magLogBase = Math.Pow(magMax, 1d / (_height - 1));
                var phaLogBase = Math.Pow(phaMax, 1d / (_height - 1));
                
                Console.WriteLine($"MAG {magMax}, {0}, {magLogBase}, Height: {_height} , Max Result: {Math.Log(1, magLogBase)}");
                Console.WriteLine($"PHA {phaMax}, {phaOffset}, {phaLogBase}, Height: {_height} , Max Result: {Math.Log(phaMax, phaLogBase) + 1}");
                
                for (var x = 0; x < (float)_width; x++)
                {
                    for (var y = 0f; y < _height; y++)
                    {
                        #region Magnitude

                        #region Red
                        
                        var rMagValue = rMag[x, (int)y] + 1;
                        var rMagScaledValue = (int)Math.Floor(Math.Log(rMagValue, magLogBase));
                        if (rMagScaledValue < 0)
                            rMagScaledValue = Math.Abs(rMagScaledValue);
                        rMagScaledValue++;
                        
                        if (rMagScaledValue > _height || rMagScaledValue == 0)
                            Console.WriteLine($"RMAG {rMagValue}, {rMagScaledValue}");
                        var color = analysisMagImage[x, _height - rMagScaledValue];

                        analysisMagImage[x, _height - rMagScaledValue] = Color.FromRgb(255, color.G, color.B);
                        
                        #endregion

                        #region Green
                        
                        var gMagValue = gMag[x, (int)y] + 1;
                        var gMagScaledValue = (int)Math.Floor(Math.Log(gMagValue, magLogBase));
                        if (gMagScaledValue < 0)
                            gMagScaledValue = Math.Abs(gMagScaledValue);
                        gMagScaledValue++;
                        
                        if (gMagScaledValue > _height || gMagScaledValue == 0)
                            Console.WriteLine($"GMAG {gMagValue}, {gMagScaledValue}");
                        color = analysisMagImage[x, _height - gMagScaledValue];

                        analysisMagImage[x, _height - gMagScaledValue] = Color.FromRgb(color.R, 255, color.B);
                        
                        #endregion

                        #region Blue
                        
                        var bMagValue = bMag[x, (int)y] + 1;
                        var bMagScaledValue = (int)Math.Floor(Math.Log(bMagValue, magLogBase));
                        if (bMagScaledValue < 0)
                            bMagScaledValue = Math.Abs(bMagScaledValue);
                        bMagScaledValue++;
                        
                        if (bMagScaledValue > _height || bMagScaledValue == 0)
                            Console.WriteLine($"BMAG {bMagValue}, {bMagScaledValue}");
                        color = analysisMagImage[x, _height - bMagScaledValue];

                        analysisMagImage[x, _height - bMagScaledValue] = Color.FromRgb(color.R, color.G, 255);
                        
                        #endregion

                        #endregion
                        
                        #region Phase

                        #region Red
                        
                        var rPhaValue = rPha[x, (int)y] + phaOffset;
                        var rPhaScaledValue = (int)Math.Floor(Math.Log(rPhaValue, phaLogBase));
                        if (rPhaScaledValue < 0)
                            rPhaScaledValue = Math.Abs(rPhaScaledValue);
                        rPhaScaledValue++;
                        
                        if (rPhaScaledValue > _height || rPhaScaledValue == 0)
                            Console.WriteLine($"RPHA {rPhaValue}, {rPhaScaledValue}");
                        
                        color = analysisPhaImage[x, _height - rPhaScaledValue];

                        analysisPhaImage[x, _height - rPhaScaledValue] = Color.FromRgb(255, color.G, color.B);
                        
                        #endregion

                        #region Green
                        
                        var gPhaValue = gPha[x, (int)y] + phaOffset;
                        var gPhaScaledValue = (int)Math.Floor(Math.Log(gPhaValue, phaLogBase));
                        if (gPhaScaledValue < 0)
                            gPhaScaledValue = Math.Abs(gPhaScaledValue);
                        gPhaScaledValue++;
                        
                        if (gPhaScaledValue > _height || gPhaScaledValue == 0)
                            Console.WriteLine($"GPHA {gPhaValue}, {gPhaScaledValue}");
                        color = analysisPhaImage[x, _height - gPhaScaledValue];

                        analysisPhaImage[x, _height - gPhaScaledValue] = Color.FromRgb(color.R, 255, color.B);
                        
                        #endregion

                        #region Blue
                        var bPhaValue = bPha[x, (int)y] + phaOffset;
                        var bPhaScaledValue = (int)Math.Floor(Math.Log(bPhaValue, phaLogBase));
                        if (bPhaScaledValue < 0)
                            bPhaScaledValue = Math.Abs(bPhaScaledValue);
                        bPhaScaledValue++;
                        
                        if (bPhaScaledValue > _height || bPhaScaledValue == 0)
                            Console.WriteLine($"BPHA {bPhaValue}, {bPhaScaledValue}");
                        color = analysisPhaImage[x, _height - bPhaScaledValue];

                        analysisPhaImage[x, _height - bPhaScaledValue] = Color.FromRgb(color.R, color.G, 255);
                        
                        #endregion

                        #endregion
                    }

                }
                
                analysisMagImage.SaveAsync($"Images/{name}/analysis_Mag.png");
                analysisPhaImage.SaveAsync($"Images/{name}/analysis_Pha.png");
                
                break;
            }
        }
            
        
        /*// dimension size * channels (3) * 2 (mag/pha) + 8 (2 32-bit ints for width / height)
        var bytes = new byte[(dimSize * 3 * 2) + 8];

        Array.Copy(BitConverter.GetBytes(_width), 0, bytes, 0, 4);
        Array.Copy(BitConverter.GetBytes(_height), 0, bytes, 4, 4);
        
        
        var outputImage = new Image<Rgba32>(_width, _height, Color.Black);
        
        var i = 0;
        for (var x = 0; x < _width; x++)
        {
            for (var y = 0; y < _height; y++)
            {
                Array.Copy(BitConverter.GetBytes(
                    data.RMag()[x, y]), 0, bytes,
                    i + (dimSize * 0) + 8, 8);
                Array.Copy(BitConverter.GetBytes(
                    data.GMag()[x, y]), 0, bytes,
                    i + (dimSize * 1) + 8, 8);
                Array.Copy(BitConverter.GetBytes(
                    data.BMag()[x, y]), 0, bytes,
                    i + (dimSize * 2) + 8, 8);
                Array.Copy(BitConverter.GetBytes(
                    data.RPha()[x, y]), 0, bytes,
                    i + (dimSize * 3) + 8, 8);
                Array.Copy(BitConverter.GetBytes(
                    data.GPha()[x, y]), 0, bytes,
                    i + (dimSize * 4) + 8, 8);
                Array.Copy(BitConverter.GetBytes(
                    data.BPha()[x, y]), 0, bytes,
                    i + (dimSize * 5) + 8, 8);
                
                var reCol = Color.FromRgb(
                    (byte)data.RCom()[x, y].Real,
                    (byte)data.GCom()[x, y].Real,
                    (byte)data.BCom()[x, y].Real);
                
                outputImage[x, y] = reCol;

                i += 8;
            }
        }*/
        
        Console.WriteLine("Done");
            
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
        
        Console.WriteLine("iFFTs");

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
        
        Console.WriteLine("iFFTs Complete");



            
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

    /*
    #region Accelerated algorithm


    static int[][,] CreateImage(Accelerator accelerator, int[,] r, int[,] g, int[,] b)
    {
        var kernel = accelerator.LoadAutoGroupedStreamKernel<
            Index2D,
            int,
            int,
            ArrayView2D<int, Stride2D.DenseX>,
            ArrayView2D<int, Stride2D.DenseX>,
            ArrayView2D<int, Stride2D.DenseX>,

            ArrayView2D<int, Stride2D.DenseX>,
            ArrayView2D<int, Stride2D.DenseX>,
            ArrayView2D<int, Stride2D.DenseX>
        >(CreateImageKernel);

        var width = r.GetLength(0);
        var height = r.GetLength(1);

        using var rBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(width, height));
        using var gBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(width, height));
        using var bBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(width, height));

        using var rOutBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(width, height));
        using var gOutBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(width, height));
        using var bOutBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(width, height));

        rBuffer.CopyFromCPU(r);
        gBuffer.CopyFromCPU(g);
        bBuffer.CopyFromCPU(b);

        kernel(rBuffer.Extent.ToIntIndex(), r.GetLength(0), r.GetLength(1), rBuffer.View, gBuffer.View, bBuffer.View, rOutBuffer.View, gOutBuffer.View, bOutBuffer.View);
            
        int[][,] outputPackage = 
        {
            rOutBuffer.GetAsArray2D(),
            gOutBuffer.GetAsArray2D(),
            bOutBuffer.GetAsArray2D()
        };

        return outputPackage;

    }
        
    static void CreateImageKernel(
        Index2D index,
        int width,
        int height,
        ArrayView2D<int, Stride2D.DenseX> r,
        ArrayView2D<int, Stride2D.DenseX> g,
        ArrayView2D<int, Stride2D.DenseX> b,

        ArrayView2D<int, Stride2D.DenseX> rOut,
        ArrayView2D<int, Stride2D.DenseX> gOut,
        ArrayView2D<int, Stride2D.DenseX> bOut
    )
    {

        rOut[index] = (int)(Math.Sin(index.X + (index.Y * height)) * 128);
        gOut[index] = (int)(Math.Sin(index.X + (index.Y * height)) * 128);
        bOut[index] = (int)(Math.Sin(index.X + (index.Y * height)) * 128);

        /*
        if (index.X <= 2 || index.X >= r.IntExtent.X-2 || index.Y <= 2 || index.Y >= r.IntExtent.Y-2)
        {
            rOut[index] = 0;
            gOut[index] = 0;
            bOut[index] = 0;
        }
        else
        {
            var rAverage = (r[index.X, index.Y + 1] + r[index.X + 1, index.Y] + r[index.X + 1, index.Y + 1] + r[index.X, index.Y - 1] + r[index.X - 1, index.Y] + r[index.X - 1, index.Y - 1] + r[index.X + 1, index.Y - 1] + r[index.X - 1, index.Y + 1]) / 8;
            var gAverage = (g[index.X, index.Y + 1] + g[index.X + 1, index.Y] + g[index.X + 1, index.Y + 1] + g[index.X, index.Y - 1] + g[index.X - 1, index.Y] + g[index.X - 1, index.Y - 1] + g[index.X + 1, index.Y - 1] + g[index.X - 1, index.Y + 1]) / 8;
            var bAverage = (b[index.X, index.Y + 1] + b[index.X + 1, index.Y] + b[index.X + 1, index.Y + 1] + b[index.X, index.Y - 1] + b[index.X - 1, index.Y] + b[index.X - 1, index.Y - 1] + b[index.X + 1, index.Y - 1] + b[index.X - 1, index.Y + 1]) / 8;

            var rDiff = Difference(rAverage, r[index]) / 16;
            var gDiff = Difference(gAverage, g[index]) / 16;
            var bDiff = Difference(bAverage, b[index]) / 16;


            double averageDiff = (rDiff + gDiff + bDiff) / 3.0;


            rOut[index] = (int)(r[index] * averageDiff);
            gOut[index] = (int)(g[index] * averageDiff);
            bOut[index] = (int)(b[index] * averageDiff);


            rOut[index] = (int)(averageDiff * 256);
            gOut[index] = (int)(averageDiff * 256);
            bOut[index] = (int)(averageDiff * 256);


        }
        

    }

    #endregion
    
    */

    #region Helpers
    
    private static string CharGen(int count, string character)
    {
        var output = "";
        for (var i = 0; i < count; i++)
        {
            output += character;
        }
        return output;
    }

    private static string BeautifyInt(int input, int length)
    {
        var inputLength = input.ToString().Length;
        if (inputLength >= length) return input.ToString();
        var diff = length - inputLength;
        return CharGen(diff, " ") + input;

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