using UnityEngine;
using System.Numerics; // Requires .NET 4.x or higher in Unity Player Settings

public static class FFTUtility
{
    // Applies a Hamming window to reduce spectral leakage
    public static float[] GetSpectrum(float[] samples)
    {
        int length = samples.Length;
        Complex[] complexSamples = new Complex[length];

        // 1. Apply Windowing (Hanning/Hamming) & Convert to Complex
        for (int i = 0; i < length; i++)
        {
            // Hanning Window formula
            float window = 0.5f * (1f - Mathf.Cos(2 * Mathf.PI * i / (length - 1)));
            complexSamples[i] = new Complex(samples[i] * window, 0);
        }

        // 2. Perform FFT
        FFT(complexSamples);

        // 3. Calculate Magnitude (Spectrum)
        // We only need the first half (Nyquist frequency)
        float[] spectrum = new float[length / 2];
        for (int i = 0; i < length / 2; i++)
        {
            spectrum[i] = (float)complexSamples[i].Magnitude;
        }

        return spectrum;
    }

    // Standard Cooley-Tukey FFT Algorithm
    private static void FFT(Complex[] data)
    {
        int n = data.Length;
        int m = (int)Mathf.Log(n, 2);

        // Bit reversal
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                var temp = data[i];
                data[i] = data[j];
                data[j] = temp;
            }
            int k = n / 2;
            while (k <= j)
            {
                j -= k;
                k /= 2;
            }
            j += k;
        }

        // Butterfly operations
        for (int s = 1; s <= m; s++)
        {
            int m2 = 1 << s;
            int m1 = m2 / 2;
            Complex w_m = Complex.FromPolarCoordinates(1, -2 * Mathf.PI / m2);

            for (int k = 0; k < n; k += m2)
            {
                Complex w = 1;
                for (int x = 0; x < m1; x++)
                {
                    Complex t = w * data[k + x + m1];
                    Complex u = data[k + x];
                    data[k + x] = u + t;
                    data[k + x + m1] = u - t;
                    w *= w_m;
                }
            }
        }
    }
}