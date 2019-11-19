﻿using System;

namespace J2N
{
    /// <summary>
    /// This class provides methods that generates pseudo-random numbers of different
    /// types, such as <see cref="int"/>, <see cref="long"/>, <see cref="double"/>, and <see cref="float"/>.
    /// <para/>
    /// Usage Note:
    /// <para/>
    /// This class differs from <see cref="System.Random"/> in the following ways:
    /// <list type="bullet">
    ///     <item><description>It uses the same pseudo-random algorithm that is used in Java, so setting
    ///         the seed to the same value on both platforms produces identical results.</description></item>
    ///     <item><description>The random seed is provided as <see cref="long"/> rather than <see cref="int"/>.</description></item>
    ///     <item><description>The random seed can be set again after the instance is created using <see cref="Seed"/>.
    ///         This sets the instance back to the same state as if it were newly created with that seed value.</description></item>
    ///     <item><description>It provides random values for <see cref="bool"/>, <see cref="int"/>,
    ///         <see cref="long"/>, <see cref="double"/>, and <see cref="float"/> as well as a 
    ///         <see cref="NextGaussian()"/> method.</description></item>
    ///     <item><description>Random number generation is thread-safe.</description></item>
    /// </list>
    /// <para/>
    /// This class differs from <c>java.util.Random</c> in the following ways:
    /// <list type="bullet">
    ///     <item><description>It subclasses <see cref="System.Random"/> (so it can be used interchangably in .NET).</description></item>
    ///     <item><description>It provides the <see cref="Next(int, int)"/> overload.</description></item>
    ///     <item><description>It doesn't use the <see cref="Randomizer"/> instance for thread synchronization, instead it
    ///         exposes its lock object through its <see cref="SyncRoot"/> property.</description></item>
    ///     <item><description>Its initial seed can be read (as well as set) through the <see cref="Seed"/> property.</description></item>
    ///     <item><description>The <c>nextInt()</c> methods were renamed <see cref="Next()"/> and <see cref="Next(int)"/> to
    ///         override the <see cref="System.Random"/> methods.</description></item>
    ///     <item><description>The <c>next(int)</c> method was renamed <see cref="NextInt(int)"/>. Keep this in mind when subclassing.</description></item>
    /// </list>
    /// </summary>
#if FEATURE_SERIALIZABLE
    [Serializable]
#endif
    public class Randomizer : System.Random
    {
        private const long multiplier = 0x5deece66dL;

#if FEATURE_SERIALIZABLE
        [NonSerialized]
#endif
        private readonly object syncRoot = new object();

        /// <summary>
        /// The backing field for the user. This is the value the user sets, and
        /// is the value returned from the <see cref="Seed"/> property, however <see cref="internalSeed"/>
        /// is the actual value used to generate random numbers.
        /// </summary>
        private long seed;

        /// <summary>
        /// The boolean value indicating if the second Gaussian number is available.
        /// </summary>
        private bool haveNextNextGaussian;

        /// <summary>
        /// It is associated with the internal state of this generator.
        /// </summary>
        private long internalSeed;

        /// <summary>
        /// The second Gaussian generated number.
        /// </summary>
        private double nextNextGaussian;

        /// <summary>
        /// Construct a random generator with the current time of day in milliseconds
        /// as the initial state.
        /// </summary>
        /// <seealso cref="Seed"/>
        public Randomizer()
        {
            Seed = Time.CurrentTimeMilliseconds() + GetHashCode();
        }

        /// <summary>
        /// Construct a random generator with the given <paramref name="seed"/> as the
        /// initial state.
        /// </summary>
        /// <param name="seed">The seed that will determine the initial state of this random
        /// number generator.</param>
        /// <seealso cref="Seed"/>
        public Randomizer(long seed)
        {
            Seed = seed;
        }

        /// <summary>
        /// Returns a pseudo-random uniformly distributed <see cref="int"/> value of
        /// the number of bits specified by the argument <paramref name="bits"/> as
        /// described by Donald E. Knuth in <i>The Art of Computer Programming,
        /// Volume 2: Seminumerical Algorithms</i>, section 3.2.1.
        /// <para/>
        /// NOTE: This was next() in Java.
        /// </summary>
        /// <param name="bits">Number of bits of the returned value.</param>
        /// <returns>A pseudo-random generated int number.</returns>
        /// <seealso cref="NextBytes(byte[])"/>
        /// <seealso cref="NextDouble()"/>
        /// <seealso cref="NextSingle()"/>
        /// <seealso cref="Next()"/>
        /// <seealso cref="Next(int)"/>
        /// <seealso cref="Next(int, int)"/>
        /// <seealso cref="NextGaussian()"/>
        /// <seealso cref="NextInt64()"/>
        protected virtual int NextInt(int bits)
        {
            lock (syncRoot)
            {
                internalSeed = (internalSeed * multiplier + 0xbL) & ((1L << 48) - 1);
                return (int)(internalSeed.TripleShift(48 - bits));
            }
        }

        /// <summary>
        /// Returns the next pseudo-random, uniformly distributed <see cref="bool"/> value
        /// generated by this generator.
        /// </summary>
        /// <returns>A pseudo-random, uniformly distributed <see cref="bool"/> value.</returns>
        public virtual bool NextBoolean()
        {
            return NextInt(1) != 0;
        }

        /// <summary>
        /// Modifies the <see cref="byte"/> array by a random sequence of <see cref="byte"/>s generated by this
        /// random number generator.
        /// </summary>
        /// <param name="buffer">Array to contain the new random <see cref="byte"/>s.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="buffer"/> is <c>null</c>.</exception>
        /// <seealso cref="NextInt(int)"/>
        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            int rand = 0, count = 0, loop = 0;
            while (count < buffer.Length)
            {
                if (loop == 0)
                {
                    rand = Next();
                    loop = 3;
                }
                else
                {
                    loop--;
                }
                buffer[count++] = (byte)rand;
                rand >>= 8;
            }
        }

        /// <summary>
        /// Generates a normally distributed random <see cref="double"/> number between 0.0
        /// inclusively and 1.0 exclusively.
        /// </summary>
        /// <returns>A random <see cref="double"/> in the range [0.0 - 1.0).</returns>
        /// <seealso cref="NextSingle()"/>
        public override double NextDouble()
        {
            return ((((long)NextInt(26) << 27) + NextInt(27)) / (double)(1L << 53));
        }

        /// <summary>
        /// Generates a normally distributed random <see cref="float"/> number between 0.0
        /// inclusively and 1.0 exclusively.
        /// </summary>
        /// <returns>A random <see cref="float"/> number between [0.0 and 1.0).</returns>
        /// <seealso cref="NextDouble()"/>
        public virtual float NextSingle()
        {
            return (NextInt(24) / 16777216f);
        }

        /// <summary>
        /// Pseudo-randomly generates (approximately) a normally distributed
        /// <see cref="double"/> value with mean 0.0 and a standard deviation value
        /// of <c>1.0</c> using the <i>polar method</i> of G. E. P. Box, M.
        /// E. Muller, and G. Marsaglia, as described by Donald E. Knuth in <i>The
        /// Art of Computer Programming, Volume 2: Seminumerical Algorithms</i>,
        /// section 3.4.1, subsection C, algorithm P.
        /// </summary>
        /// <returns>A random <see cref="double"/>.</returns>
        /// <seealso cref="NextDouble()"/>
        public virtual double NextGaussian()
        {
            lock (syncRoot)
            {
                if (haveNextNextGaussian)
                { // if X1 has been returned, return the
                  // second Gaussian
                    haveNextNextGaussian = false;
                    return nextNextGaussian;
                }

                double v1, v2, s;
                do
                {
                    v1 = 2 * NextDouble() - 1; // Generates two independent random
                                               // variables U1, U2
                    v2 = 2 * NextDouble() - 1;
                    s = v1 * v1 + v2 * v2;
                } while (s >= 1);
                double norm = Math.Sqrt(-2 * Math.Log(s) / s);
                nextNextGaussian = v2 * norm; // should that not be norm instead
                                              // of multiplier ?
                haveNextNextGaussian = true;
                return v1 * norm; // should that not be norm instead of multiplier
                                  // ?
            }
        }

        /// <summary>
        /// Generates a non-negative uniformly distributed 32-bit <see cref="int"/> value from
        /// the random number sequence.
        /// <para/>
        /// NOTE: This was nextInt() in Java.
        /// </summary>
        /// <returns>A uniformly distributed <see cref="int"/> value.</returns>
        /// <seealso cref="int.MaxValue"/>
        /// <seealso cref="int.MinValue"/>
        /// <seealso cref="NextInt(int)"/>
        /// <seealso cref="NextInt64()"/>;
        public override int Next()
        {
            return NextInt(32);
        }

        /// <summary>
        /// Returns a new non-negative pseudo-random <see cref="int"/> value which is uniformly distributed
        /// between 0 (inclusively) and the value of <paramref name="maxValue"/> (exclusively).
        /// <para/>
        /// NOTE: This was nextInt(int) in Java.
        /// </summary>
        /// <param name="maxValue">The exclusive upper bound of the range.</param>
        /// <returns>A random <see cref="int"/>.</returns>
        public override int Next(int maxValue)
        {
            if (maxValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue));

            return Next(0, maxValue);
        }

        /// <summary>
        /// Returns a new non-negative pseudo-random <see cref="int"/> value which is uniformly distributed
        /// between <paramref name="minValue"/> (inclusively) and the value of <paramref name="maxValue"/> (exclusively).
        /// <para/>
        /// NOTE: This was nextInt(int, int) in Java.
        /// </summary>
        /// <param name="minValue">The inclusive lower bound of the range.</param>
        /// <param name="maxValue">The exclusive upper bound of the range. <paramref name="maxValue"/>
        /// must be greater than or equal to <paramref name="minValue"/>.</param>
        /// <returns>A random <see cref="int"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(minValue), $"{minValue} must be less than {maxValue}.");

            if ((maxValue & -maxValue) == maxValue)
            {
                return (int)((maxValue * (long)NextInt(31)) >> 31);
            }
            int bits, val;
            do
            {
                bits = NextInt(31);
                val = bits % maxValue;
            } while (bits - val + (maxValue - 1) < 0 || val < minValue);
            return val;
        }

        /// <summary>
        /// Generates a uniformly distributed 64-bit integer value from
        /// the random number sequence.
        /// <para/>
        /// NOTE: This was nextLong() in Java.
        /// </summary>
        /// <returns>64-bit random integer.</returns>
        /// <seealso cref="long.MaxValue"/>
        /// <seealso cref="long.MinValue"/>
        /// <seealso cref="NextInt(int)"/>
        /// <seealso cref="Next()"/>
        /// <seealso cref="Next(int)"/>
        /// <seealso cref="Next(int, int)"/>
        public virtual long NextInt64()
        {
            return ((long)NextInt(32) << 32) + NextInt(32);
        }

        /// <summary>
        /// Gets or sets the seed using linear congruential formula presented in <i>The
        /// Art of Computer Programming, Volume 2</i>, Section 3.2.1.
        /// <para/>
        /// Although the seed is exposed here as an instance member that doesn't change,
        /// its value reflects the original seed that was set, not the current state of the seed
        /// that is used for the next operation. This allows the initial seed to be read regardless
        /// of the current state of <see cref="Randomizer"/>, so it can be set again to produce repeatable
        /// results.
        /// <para/>
        /// Setting the <see cref="Seed"/> resets the state of <see cref="Randomizer"/> to the same
        /// state as creating a new <see cref="Randomizer"/> instance with the same seed value passed to
        /// its constructor.
        /// <para/>
        /// Note that while <see cref="Randomizer"/> is thread safe, getting and setting
        /// (or using operators) on <see cref="Seed"/> are not atomic. To synchronize,
        /// use the <see cref="SyncRoot"/> property. Example:
        /// <code>
        /// Random random = new Random(123);
        /// lock (random.SyncRoot)
        ///     random.Seed++;
        /// </code>
        /// which is equivalent to
        /// <code>
        /// Random random = new Random(123);
        /// lock (random.SyncRoot)
        ///     random.Seed = random.Seed + 1;
        /// </code>
        /// that is, both operations require a get, an addition, and then a set. So,
        /// the lock is necessary to ensure the 3 operations happen atomically.
        /// </summary>
        /// <seealso cref="Randomizer.Randomizer()"/>
        /// <seealso cref="Randomizer.Randomizer(long)"/>
        /// <seealso cref="Randomizer.SyncRoot"/>
        // J2N: This property setter has a side-effect, but its usage expectation is clear
        // that seed will reset the state of the entire random number generator.
        public long Seed
        {
            get
            {
                lock (syncRoot)
                    return seed;
            }
            set
            {
                lock (syncRoot)
                {
                    this.seed = value;
                    this.internalSeed = (this.seed ^ multiplier) & ((1L << 48) - 1);
                    haveNextNextGaussian = false;
                }
            }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="Randomizer"/>.
        /// </summary>
        /// <seealso cref="Seed"/>
        public object SyncRoot => syncRoot;
    }
}
