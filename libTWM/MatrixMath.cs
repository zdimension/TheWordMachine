using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace libTWM
{
    internal class MatrixMath
    {
        private readonly int matSize;

        public MatrixMath(int size)
        {
            matSize = size;
        }

        public int[] Sum(int[][] arr)
        {
            var result = new int[matSize];

            Parallel.For(0, matSize, i =>
            {
                var s = 0;

                for (var j = 0; j < matSize; j++)
                {
                    s += arr[j][i];
                }

                result[i] = s;
            });

            return result;
        }

        public int[][] SumAxis0(int[][][] arr)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new int[matSize];

                Parallel.For(0, matSize, j =>
                {
                    var s = 0;

                    for (var k = 0; k < matSize; k++)
                    {
                        s += arr[k][i][j];
                    }

                    result[i][j] = s;
                });
            });

            return result;
        }

        public int[][] SumAxis2(int[][][] arr)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new int[matSize];

                Parallel.For(0, matSize, j =>
                {
                    var s = 0;

                    for (var k = 0; k < matSize; k++)
                    {
                        s += arr[i][j][k];
                    }

                    result[i][j] = s;
                });
            });

            return result;
        }

        public int[][] Transpose2(int[][] array)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new int[matSize];

                Parallel.For(0, matSize, j =>
                {
                    result[i][j] = array[j][i];
                });
            });

            return result;
        }

        public int[][][] Transpose3(int[][][] array)
        {
            var result = new int[matSize][][];

            Parallel.For(0, matSize, x =>
            {
                result[x] = new int[matSize][];

                Parallel.For(0, matSize, y =>
                {
                    result[x][y] = new int[matSize];

                    Parallel.For(0, matSize, z =>
                    {
                        result[x][y][z] = array[z][y][x];
                    });
                });
            });

            return result;
        }

        public int[][] Tile2(int[] array)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = array.ToArray();
            });

            return result;
        }

        public int[][][] Tile3(int[][] array)
        {
            var result = new int[matSize][][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = array.Select(a => a.ToArray()).ToArray();
            });

            return result;
        }

        public float[][] Divide(int[][] a, int[][] b)
        {
            var result = new float[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new float[matSize];

                Parallel.For(0, matSize, j =>
                {
                    var b1 = b[i][j];
                    if (b1 != 0) result[i][j] = (float)a[i][j] / b1;
                });
            });

            return result;
        }

        public float[][][] Divide(int[][][] a, int[][][] b)
        {
            var result = new float[matSize][][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new float[matSize][];

                Parallel.For(0, matSize, j =>
                {
                    result[i][j] = new float[matSize];

                    Parallel.For(0, matSize, k =>
                    {
                        var b1 = b[i][j][k];
                        if (b1 != 0) result[i][j][k] = (float)a[i][j][k] / b1;
                    });
                });
            });

            return result;
        }

        public float[][] Pow(float[][] a, float f)
        {
            var result = new float[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new float[matSize];

                Parallel.For(0, matSize, j =>
                {
                    result[i][j] = (float)Math.Pow(a[i][j], f);
                });
            });

            return result;
        }
    }
}
