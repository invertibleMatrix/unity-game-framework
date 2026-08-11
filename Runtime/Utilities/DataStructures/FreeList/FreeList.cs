using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace AK.Utilities.DataStructures
{
    public class FreeList<T>
    {
        private static int _listEndHead = Int32.MaxValue;

        private List<ListEntry> _objects = new();
        private uint            _objectCount;
        private int             _freeListHead = Int32.MaxValue;

        public Handle<T> Add(T item)
        {
            ListEntry listEntry = new ListEntry(item);
            int idx = 0;
            if (_freeListHead != _listEndHead)
            {
                idx = _freeListHead;
                _freeListHead = _objects[idx].NextFree;
                ListEntry temp = _objects[idx];
                temp.Item = item;
                _objects[idx] = temp;
            }
            else
            {
                idx = _objects.Count;
                _objects.Add(listEntry);
            }

            _objectCount++;
            return new Handle<T>(idx, _objects[idx].Generation);
        }

        public void Remove(Handle<T> handle)
        {
            Assert.IsTrue(_objectCount > 0);
            Assert.IsTrue(handle.Index < _objects.Count);
            Assert.IsTrue(handle.Generation == _objects[handle.Index].Generation);
            ListEntry obj = _objects[handle.Index];
            obj.Item = default(T);
            obj.Generation++;
            obj.NextFree = _freeListHead;
            _freeListHead = handle.Index;
            _objectCount--;
        }

        public T Get(Handle<T> handle)
        {
            Assert.IsTrue(_objectCount > 0);
            Assert.IsTrue(handle.Index < _objects.Count);
            Assert.IsTrue(handle.Generation == _objects[handle.Index].Generation);
            return _objects[handle.Index].Item;
        }

        private struct ListEntry
        {
            public T    Item;
            public int  NextFree;
            public uint Generation;

            public ListEntry(T item)
            {
                Item = item;
                NextFree = _listEndHead;
                Generation = 1;
            }
        }
    }
}