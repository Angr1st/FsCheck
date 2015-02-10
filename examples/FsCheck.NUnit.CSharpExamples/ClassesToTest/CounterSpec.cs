﻿using System;

namespace FsCheck.NUnit.CSharpExamples.ClassesToTest
{
    public class CounterSpec : Commands.ISpecification<Counter, int>
    {
        public Gen<Commands.ICommand<Counter, int>> GenCommand(int value)
        {
            return Gen.Elements(new Commands.ICommand<Counter, int>[] {new Inc(), new Dec()});
        }

        public Tuple<Counter, int> Initial()
        {
            return new Tuple<Counter, int>(new Counter(), 0);
        }

        private class Inc : BaseCommand
        {
            public override Counter RunActual(Counter c)
            {
                c.Inc();
                return c;
            }

            public override int RunModel(int m)
            {
                return m + 1;
            }
        }

        private class Dec : BaseCommand
        {
            public override Counter RunActual(Counter c)
            {
                c.Dec();
                return c;
            }

            public override int RunModel(int m)
            {
                return m - 1;
            }
        }

        private abstract class BaseCommand : Commands.ICommand<Counter, int>
        {
            public override Gen<Rose<Result>> Post(Counter c, int m)
            {
                return Prop.ofTestable(m == c.Get());
            }

            public override string ToString()
            {
                return GetType().Name;
            }
        }
    }
}