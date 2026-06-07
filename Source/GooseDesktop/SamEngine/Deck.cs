using System;

namespace SamEngine
{
    internal class Deck
    {
        public Deck(int Length)
        {
            this.indices = new int[Length];
            this.Reshuffle();
        }

        public void Reshuffle()
        {
            for (int i = 0; i < this.indices.Length; i++)
            {
                this.indices[i] = i;
                int num = (int)SamMath.RandomRange(0f, (float)i);
                int num2 = this.indices[i];
                this.indices[i] = this.indices[num];
                this.indices[num] = num2;
            }
        }

        public int Next()
        {
            int result = this.indices[this.i];
            this.i++;
            if (this.i >= this.indices.Length)
            {
                this.Reshuffle();
                this.i = 0;
            }
            return result;
        }

        public int[] indices;

        private int i;
    }
}
