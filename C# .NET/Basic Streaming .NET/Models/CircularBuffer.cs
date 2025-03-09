using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Basic_Streaming_NET.Models
{
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _tail;
        private int _count;

        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public int Capacity => _buffer.Length;

        public int Count => _count;

        public void Add(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % Capacity;

            if (_count < Capacity)
            {
                _count++;
            }
            else
            {
                _tail = (_tail + 1) % Capacity; // 覆蓋舊數據
            }
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException("Index out of range");

                int actualIndex = (_tail + index) % Capacity;
                return _buffer[actualIndex];
            }
        }

        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public List<T> ToList()
        {
            List<T> list = new List<T>();
            for (int i = 0; i < _count; i++)
            {
                list.Add(this[i]);
            }
            return list;
        }
    }
}
