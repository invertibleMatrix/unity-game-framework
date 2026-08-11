namespace AK.Utilities.DataStructures
{
    public struct Handle<T>
    {
        public int  Index;
        public uint Generation;

        public Handle(int index, uint generation)
        {
            Index = index;
            Generation = generation;
        }
    }
}