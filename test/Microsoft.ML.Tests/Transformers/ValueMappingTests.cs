﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.Data;
using Microsoft.ML.Model;
using Microsoft.ML.RunTests;
using Microsoft.ML.Tools;
using Microsoft.ML.Transforms;
using Microsoft.ML.Transforms.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.ML.Tests.Transformers
{
    public class ValueMappingTests : TestDataPipeBase
    {
        public ValueMappingTests(ITestOutputHelper output) : base(output)
        {
        }

        class TestClass
        {
            public string A;
            public string B;
            public string C;
        }

        class TestWrong
        {
            public string A;
            public float B;
        }

        public class TestTermLookup
        {
            public string Label;
            public int GroupId;

            [VectorType(2107)]
            public float[] Features;
        };


        [Fact]
        public void ValueMapOneValueTest()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keys = new List<string>() { "foo", "bar", "test", "wahoo" };
            var values = new List<int>() { 1, 2, 3, 4 };

            var lookupMap = DataViewHelper.CreateDataView(Env, keys, values,
                ValueMappingTransformer.DefaultKeyColumnName,
                ValueMappingTransformer.DefaultValueColumnName, false);

            var estimator = new ValueMappingEstimator<string, int>(Env, lookupMap,
                lookupMap.Schema[ValueMappingTransformer.DefaultKeyColumnName],
                lookupMap.Schema[ValueMappingTransformer.DefaultValueColumnName],
                new[] { ("D", "A"), ("E", "B"), ("F", "C") });

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<int>(result.Schema["D"]);
            var getterE = cursor.GetGetter<int>(result.Schema["E"]);
            var getterF = cursor.GetGetter<int>(result.Schema["F"]);
            cursor.MoveNext();

            int dValue = 0;
            getterD(ref dValue);
            Assert.Equal(2, dValue);
            int eValue = 0;
            getterE(ref eValue);
            Assert.Equal(3, eValue);
            int fValue = 0;
            getterF(ref fValue);
            Assert.Equal(1, fValue);
        }

        [Fact]
        public void ValueMapInputIsVectorTest()
        {
            var data = new[] { new TestClass() { A = "bar test foo", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keys = new List<ReadOnlyMemory<char>>() { "foo".AsMemory(), "bar".AsMemory(), "test".AsMemory(), "wahoo".AsMemory() };
            var values = new List<int>() { 1, 2, 3, 4 };

            var lookupMap = DataViewHelper.CreateDataView(Env, keys, values,
                ValueMappingTransformer.DefaultKeyColumnName,
                ValueMappingTransformer.DefaultValueColumnName, false);

            var valueMappingEstimator = new ValueMappingEstimator<string, int>(Env, lookupMap,
                lookupMap.Schema[ValueMappingTransformer.DefaultKeyColumnName],
                lookupMap.Schema[ValueMappingTransformer.DefaultValueColumnName],
                new[] { ("VecD", "TokenizeA"), ("E", "B"), ("F", "C") });

            var estimator = new WordTokenizingEstimator(Env, new[]{
                    new WordTokenizingEstimator.ColumnOptions("TokenizeA", "A")
                }).Append(valueMappingEstimator);

            var schema = estimator.GetOutputSchema(SchemaShape.Create(dataView.Schema));
            Assert.True(schema.TryFindColumn("VecD", out var originalColumn));
            Assert.Equal(SchemaShape.Column.VectorKind.VariableVector, originalColumn.Kind);
            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterVecD = cursor.GetGetter<VBuffer<int>>(result.Schema["VecD"]);
            var getterE = cursor.GetGetter<int>(result.Schema["E"]);
            var getterF = cursor.GetGetter<int>(result.Schema["F"]);
            cursor.MoveNext();

            VBuffer<int> dValue = default;
            getterVecD(ref dValue);
            Assert.True(dValue.GetValues().SequenceEqual(new int[] { 2, 3, 1 }));

            int eValue = 0;
            getterE(ref eValue);
            Assert.Equal(3, eValue);
            int fValue = 0;
            getterF(ref fValue);
            Assert.Equal(1, fValue);
        }

        [Fact]
        public void ValueMapInputIsVectorAndValueAsStringKeyTypeTest()
        {
            var data = new[] { new TestClass() { A = "bar test foo", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keyValuePairs = new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("foo", "a"),
                new KeyValuePair<string, string>("bar", "b"),
                new KeyValuePair<string, string>("test", "c"),
                new KeyValuePair<string, string>("wahoo", "d")};

            var estimator = new WordTokenizingEstimator(Env, new[] { new WordTokenizingEstimator.ColumnOptions("TokenizeA", "A") }).
                Append(ML.Transforms.Conversion.MapValue(keyValuePairs, true, new[] { new InputOutputColumnPair("VecD", "TokenizeA"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") }));
            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterVecD = cursor.GetGetter<VBuffer<uint>>(result.Schema["VecD"]);
            var getterE = cursor.GetGetter<uint>(result.Schema["E"]);
            var getterF = cursor.GetGetter<uint>(result.Schema["F"]);
            cursor.MoveNext();

            VBuffer<uint> dValue = default;
            getterVecD(ref dValue);
            Assert.True(dValue.GetValues().SequenceEqual(new uint[] { 2, 3, 1 }));

            uint eValue = 0;
            getterE(ref eValue);
            Assert.Equal(3u, eValue);
            uint fValue = 0;
            getterF(ref fValue);
            Assert.Equal(1u, fValue);
        }

        [Fact]
        public void ValueMapVectorValueTest()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            IEnumerable<string> keys = new List<string>() { "foo", "bar", "test" };
            List<int[]> values = new List<int[]>() {
                new int[] {2, 3, 4 },
                new int[] {100, 200 },
                new int[] {400, 500, 600, 700 }};

            var lookupMap = DataViewHelper.CreateDataView(Env, keys, values,
                ValueMappingTransformer.DefaultKeyColumnName,
                ValueMappingTransformer.DefaultValueColumnName);

            var estimator = new ValueMappingEstimator<string, int>(Env, lookupMap,
                lookupMap.Schema[ValueMappingTransformer.DefaultKeyColumnName],
                lookupMap.Schema[ValueMappingTransformer.DefaultValueColumnName],
                new[] { ("D", "A"), ("E", "B"), ("F", "C") });

            var schema = estimator.GetOutputSchema(SchemaShape.Create(dataView.Schema));
            foreach (var name in new[] { "D", "E", "F" })
            {
                Assert.True(schema.TryFindColumn(name, out var originalColumn));
                Assert.Equal(SchemaShape.Column.VectorKind.VariableVector, originalColumn.Kind);
            }

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<VBuffer<int>>(result.Schema["D"]);
            var getterE = cursor.GetGetter<VBuffer<int>>(result.Schema["E"]);
            var getterF = cursor.GetGetter<VBuffer<int>>(result.Schema["F"]);
            cursor.MoveNext();

            var valuesArray = values.ToArray();
            VBuffer<int> dValue = default;
            getterD(ref dValue);
            Assert.Equal(values[1].Length, dValue.Length);
            VBuffer<int> eValue = default;
            getterE(ref eValue);
            Assert.Equal(values[2].Length, eValue.Length);
            VBuffer<int> fValue = default;
            getterF(ref fValue);
            Assert.Equal(values[0].Length, fValue.Length);
        }

        class Map
        {
            public string Key;
            public int Value;
        }

        [Fact]
        public void ValueMapDataViewAsMapTest()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var map = new[] { new Map() { Key = "foo", Value = 1 },
                              new Map() { Key = "bar", Value = 2 },
                              new Map() { Key = "test", Value = 3 },
                              new Map() { Key = "wahoo", Value = 4 }
                            };
            var mapView = ML.Data.LoadFromEnumerable(map);

            var estimator = new ValueMappingEstimator(Env, mapView, mapView.Schema["Key"], mapView.Schema["Value"], new[] { ("D", "A"), ("E", "B"), ("F", "C") });
            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<int>(result.Schema["D"]);
            var getterE = cursor.GetGetter<int>(result.Schema["E"]);
            var getterF = cursor.GetGetter<int>(result.Schema["F"]);
            cursor.MoveNext();

            int dValue = 0;
            getterD(ref dValue);
            Assert.Equal(2, dValue);
            int eValue = 0;
            getterE(ref eValue);
            Assert.Equal(3, eValue);
            int fValue = 0;
            getterF(ref fValue);
            Assert.Equal(1, fValue);
        }

        [Fact]
        public void ValueMapVectorStringValueTest()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            IEnumerable<string> keys = new List<string>() { "foo", "bar", "test" };
            List<string[]> values = new List<string[]>() {
                new string[] {"foo", "bar" },
                new string[] {"forest", "city", "town" },
                new string[] {"winter", "summer", "autumn", "spring" }};

            var lookupMap = DataViewHelper.CreateDataView(Env, keys, values,
                ValueMappingTransformer.DefaultKeyColumnName,
                ValueMappingTransformer.DefaultValueColumnName);

            var estimator = new ValueMappingEstimator<string, int>(Env, lookupMap,
                lookupMap.Schema[ValueMappingTransformer.DefaultKeyColumnName],
                lookupMap.Schema[ValueMappingTransformer.DefaultValueColumnName],
                new[] { ("D", "A"), ("E", "B"), ("F", "C") });

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);

            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<VBuffer<ReadOnlyMemory<char>>>(result.Schema[3]);
            var getterE = cursor.GetGetter<VBuffer<ReadOnlyMemory<char>>>(result.Schema[4]);
            var getterF = cursor.GetGetter<VBuffer<ReadOnlyMemory<char>>>(result.Schema[5]);
            cursor.MoveNext();

            VBuffer<ReadOnlyMemory<char>> dValue = default;
            getterD(ref dValue);
            Assert.Equal(3, dValue.Length);

            VBuffer<ReadOnlyMemory<char>> eValue = default;
            getterE(ref eValue);
            Assert.Equal(4, eValue.Length);

            VBuffer<ReadOnlyMemory<char>> fValue = default;
            getterF(ref fValue);
            Assert.Equal(2, fValue.Length);
        }

        [Fact]
        public void ValueMappingMissingKey()
        {
            var data = new[] { new TestClass() { A = "barTest", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keys = new List<string>() { "foo", "bar", "test", "wahoo" };
            var values = new List<int>() { 1, 2, 3, 4 };

            var lookupMap = DataViewHelper.CreateDataView(Env, keys, values,
                ValueMappingTransformer.DefaultKeyColumnName,
                ValueMappingTransformer.DefaultValueColumnName, false);

            var estimator = new ValueMappingEstimator<string, int>(Env, lookupMap,
                lookupMap.Schema[ValueMappingTransformer.DefaultKeyColumnName],
                lookupMap.Schema[ValueMappingTransformer.DefaultValueColumnName],
                new[] { ("D", "A"), ("E", "B"), ("F", "C") });

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<int>(result.Schema["D"]);
            var getterE = cursor.GetGetter<int>(result.Schema["E"]);
            var getterF = cursor.GetGetter<int>(result.Schema["F"]);
            cursor.MoveNext();

            int dValue = 1;
            getterD(ref dValue);
            Assert.Equal(0, dValue);
            int eValue = 0;
            getterE(ref eValue);
            Assert.Equal(3, eValue);
            int fValue = 0;
            getterF(ref fValue);
            Assert.Equal(1, fValue);
        }

        [Fact]
        public void TestDuplicateKeys()
        {
            var data = new[] { new TestClass() { A = "barTest", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keys = new List<string>() { "foo", "foo" };
            var values = new List<int>() { 1, 2 };

            var lookupMap = DataViewHelper.CreateDataView(Env, keys, values,
                ValueMappingTransformer.DefaultKeyColumnName,
                ValueMappingTransformer.DefaultValueColumnName, false);

            Assert.Throws<InvalidOperationException>(() => new ValueMappingEstimator<string, int>(Env, lookupMap,
                lookupMap.Schema[ValueMappingTransformer.DefaultKeyColumnName],
                lookupMap.Schema[ValueMappingTransformer.DefaultValueColumnName],
                new[] { ("D", "A"), ("E", "B"), ("F", "C") }));
        }

        [Fact]
        public void ValueMappingOutputSchema()
        {
            var data = new[] { new TestClass() { A = "barTest", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keyValuePairs = new List<KeyValuePair<string, int>>() {
                new KeyValuePair<string,int>("foo", 1),
                new KeyValuePair<string,int>("bar", 2),
                new KeyValuePair<string,int>("test", 3),
                new KeyValuePair<string,int>("wahoo", 4)};

            var est = ML.Transforms.Conversion.MapValue(keyValuePairs,
                new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") });

            var outputSchema = est.GetOutputSchema(SchemaShape.Create(dataView.Schema));

            Assert.Equal(6, outputSchema.Count());
            Assert.True(outputSchema.TryFindColumn("D", out SchemaShape.Column dColumn));
            Assert.True(outputSchema.TryFindColumn("E", out SchemaShape.Column eColumn));
            Assert.True(outputSchema.TryFindColumn("F", out SchemaShape.Column fColumn));

            Assert.Equal(typeof(int), dColumn.ItemType.RawType);
            Assert.False(dColumn.IsKey);

            Assert.Equal(typeof(int), eColumn.ItemType.RawType);
            Assert.False(eColumn.IsKey);

            Assert.Equal(typeof(int), fColumn.ItemType.RawType);
            Assert.False(fColumn.IsKey);
        }

        [Fact]
        public void ValueMappingWithValuesAsKeyTypesOutputSchema()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keyValuePairs = new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("foo", "t"),
                new KeyValuePair<string, string>("bar", "s"),
                new KeyValuePair<string, string>("test", "u"),
                new KeyValuePair<string, string>("wahoo", "v") };

            var estimator = ML.Transforms.Conversion.MapValue(keyValuePairs, true,
                new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") });

            var outputSchema = estimator.GetOutputSchema(SchemaShape.Create(dataView.Schema));
            Assert.Equal(6, outputSchema.Count());
            Assert.True(outputSchema.TryFindColumn("D", out SchemaShape.Column dColumn));
            Assert.True(outputSchema.TryFindColumn("E", out SchemaShape.Column eColumn));
            Assert.True(outputSchema.TryFindColumn("F", out SchemaShape.Column fColumn));

            Assert.Equal(typeof(uint), dColumn.ItemType.RawType);
            Assert.True(dColumn.IsKey);

            Assert.Equal(typeof(uint), eColumn.ItemType.RawType);
            Assert.True(eColumn.IsKey);

            Assert.Equal(typeof(uint), fColumn.ItemType.RawType);
            Assert.True(fColumn.IsKey);

            var t = estimator.Fit(dataView);
        }

        [Fact]
        public void ValueMappingValuesAsUintKeyTypes()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test2", C = "wahoo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            // These are the expected key type values
            var keyValuePairs = new List<KeyValuePair<string, uint>>() {
                new KeyValuePair<string, uint>("foo", 51),
                new KeyValuePair<string, uint>("bar", 25),
                new KeyValuePair<string, uint>("test", 42),
                new KeyValuePair<string, uint>("wahoo", 61)};

            // Workout on value mapping
            var estimator = ML.Transforms.Conversion.MapValue(keyValuePairs, true, new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") });

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<uint>(result.Schema["D"]);
            var getterE = cursor.GetGetter<uint>(result.Schema["E"]);
            var getterF = cursor.GetGetter<uint>(result.Schema["F"]);
            cursor.MoveNext();

            // The expected values will contain the actual uints and are not generated.
            uint dValue = 1;
            getterD(ref dValue);
            Assert.Equal<uint>(25, dValue);

            // Should be 0 as test2 is a missing key
            uint eValue = 0;
            getterE(ref eValue);
            Assert.Equal<uint>(0, eValue);

            // Testing the last key
            uint fValue = 0;
            getterF(ref fValue);
            Assert.Equal<uint>(61, fValue);
        }


        [Fact]
        public void ValueMappingValuesAsUlongKeyTypes()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test2", C = "wahoo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keyValuePairs = new List<KeyValuePair<string, ulong>>() {
                new KeyValuePair<string, ulong>("foo", 51),
                new KeyValuePair<string, ulong>("bar", Int32.MaxValue + 1L),
                new KeyValuePair<string, ulong>("test", 42),
                new KeyValuePair<string, ulong>("wahoo", 61)};

            // Workout on value mapping
            var estimator = ML.Transforms.Conversion.MapValue(keyValuePairs, true, new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") });

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<ulong>(result.Schema["D"]);
            var getterE = cursor.GetGetter<ulong>(result.Schema["E"]);
            var getterF = cursor.GetGetter<ulong>(result.Schema["F"]);
            cursor.MoveNext();

            // The expected values will contain the actual uints and are not generated.
            ulong dValue = 1;
            getterD(ref dValue);
            Assert.Equal<ulong>(Int32.MaxValue + 1L, dValue);

            // Should be 0 as test2 is a missing key
            ulong eValue = 0;
            getterE(ref eValue);
            Assert.Equal<ulong>(0, eValue);

            // Testing the last key
            ulong fValue = 0;
            getterF(ref fValue);
            Assert.Equal<ulong>(61, fValue);
        }

        [Fact]
        public void ValueMappingValuesAsStringKeyTypes()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "notfound" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            // Generating the list of strings for the key type values, note that foo1 is duplicated as intended to test that the same index value is returned
            var keyValuePairs = new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("foo", "foo1"),
                new KeyValuePair<string, string>("bar", "foo2"),
                new KeyValuePair<string, string>("test", "foo1"),
                new KeyValuePair<string, string>("wahoo", "foo3")};

            // Workout on value mapping
            var estimator = ML.Transforms.Conversion.MapValue(keyValuePairs, true, new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") });

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<uint>(result.Schema["D"]);
            var getterE = cursor.GetGetter<uint>(result.Schema["E"]);
            var getterF = cursor.GetGetter<uint>(result.Schema["F"]);
            cursor.MoveNext();

            // The expected values will contain the generated key type values starting from 1.
            uint dValue = 1;
            getterD(ref dValue);
            Assert.Equal<uint>(2, dValue);

            // eValue will equal 1 since foo1 occurs first.
            uint eValue = 0;
            getterE(ref eValue);
            Assert.Equal<uint>(1, eValue);

            // fValue will be 0 since its missing
            uint fValue = 0;
            getterF(ref fValue);
            Assert.Equal<uint>(0, fValue);
        }

        [Fact]
        public void ValueMappingValuesAsKeyTypesReverseLookup()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "notfound" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keyValuePairs = new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("foo", "foo1"),
                new KeyValuePair<string, string>("bar", "foo2"),
                new KeyValuePair<string, string>("test", "foo1"),
                new KeyValuePair<string, string>("wahoo", "foo3")};

            var estimator = ML.Transforms.Conversion.MapValue("D", keyValuePairs, "A", true).
                Append(ML.Transforms.Conversion.MapKeyToValue("DOutput", "D"));

            var t = estimator.Fit(dataView);

            var result = t.Transform(dataView);
            var cursor = result.GetRowCursorForAllColumns();
            var getterD = cursor.GetGetter<ReadOnlyMemory<char>>(result.Schema["DOutput"]);
            cursor.MoveNext();

            // The expected values will contain the generated key type values starting from 1.
            ReadOnlyMemory<char> dValue = default;
            getterD(ref dValue);
            Assert.Equal("foo2".AsMemory(), dValue);

            var annotations = result.Schema["D"].Annotations;
            var allowedKeyValueGetter = annotations.GetGetter<VBuffer<ReadOnlyMemory<char>>>(annotations.Schema["KeyValues"]);
            VBuffer<ReadOnlyMemory<char>> allowedKeys = default;
            allowedKeyValueGetter(ref allowedKeys);

            // There should be 3 keys, "foo1", "foo2", and "foo3".
            Assert.Equal(3, allowedKeys.Length);
            var allowedKeyPool = new HashSet<ReadOnlyMemory<char>>(allowedKeys.DenseValues());
            Assert.Contains("foo1".AsMemory(), allowedKeyPool);
            Assert.Contains("foo2".AsMemory(), allowedKeyPool);
            Assert.Contains("foo3".AsMemory(), allowedKeyPool);
        }

        [Fact]
        public void ValueMappingWorkout()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);
            var badData = new[] { new TestWrong() { A = "bar", B = 1.2f } };
            var badDataView = ML.Data.LoadFromEnumerable(badData);

            var keyValuePairs = new List<KeyValuePair<string, int>>() {
                new KeyValuePair<string, int>("foo", 1),
                new KeyValuePair<string, int>("bar", 2),
                new KeyValuePair<string, int>("test", 3),
                new KeyValuePair<string, int>("wahoo", 4)};

            // Workout on value mapping
            var est = ML.Transforms.Conversion.MapValue(keyValuePairs, new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") });
            TestEstimatorCore(est, validFitInput: dataView, invalidInput: badDataView);
        }

        [Fact]
        public void ValueMappingValueTypeIsVectorWorkout()
        {
            var data = new[] { new TestClass() { A = "bar", B = "test", C = "foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);
            var badData = new[] { new TestWrong() { A = "bar", B = 1.2f } };
            var badDataView = ML.Data.LoadFromEnumerable(badData);

            var keyValuePairs = new List<KeyValuePair<string, int[]>>() {
                new KeyValuePair<string,int[]>("foo", new int[] {2, 3, 4 }),
                new KeyValuePair<string,int[]>("bar", new int[] {100, 200 }),
                new KeyValuePair<string,int[]>("test", new int[] {400, 500, 600, 700 }),
                };

            // Workout on value mapping
            var est = ML.Transforms.Conversion.MapValue(keyValuePairs, new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B"), new InputOutputColumnPair("F", "C") });
            TestEstimatorCore(est, validFitInput: dataView, invalidInput: badDataView);
        }

        [Fact]
        public void ValueMappingInputIsVectorWorkout()
        {
            var data = new[] { new TestClass() { B = "bar test foo" } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var badData = new[] { new TestWrong() { B = 1.2f } };
            var badDataView = ML.Data.LoadFromEnumerable(badData);

            var keyValuePairs = new List<KeyValuePair<ReadOnlyMemory<char>, int>>() {
                new KeyValuePair<ReadOnlyMemory<char>,int>("foo".AsMemory(), 1),
                new KeyValuePair<ReadOnlyMemory<char>,int>("bar".AsMemory(), 2),
                new KeyValuePair<ReadOnlyMemory<char>,int>("test".AsMemory(), 3),
                new KeyValuePair<ReadOnlyMemory<char>,int>("wahoo".AsMemory(), 4)
                };

            var est = ML.Transforms.Text.TokenizeIntoWords("TokenizeB", "B")
                .Append(ML.Transforms.Conversion.MapValue("VecB", keyValuePairs, "TokenizeB"));
            TestEstimatorCore(est, validFitInput: dataView, invalidInput: badDataView);
        }

        [Fact]
        public void TestCommandLine()
        {
            var dataFile = GetDataPath("QuotingData.csv");
            Assert.Equal(Maml.Main(new[] { @"showschema loader=Text{col=A:R4:0 col=B:R4:1 col=C:R4:2} xf=valuemap{keyCol=ID valueCol=Text data="
                                    + dataFile
                                    + @" col=A:B loader=Text{col=ID:U8:0 col=Text:TX:1 sep=, header=+} } in=f:\1.txt" }), (int)0);
        }

        [Fact]
        public void TestCommandLineNoLoader()
        {
            var dataFile = GetDataPath("lm.labels.txt");
            Assert.Equal(Maml.Main(new[] { @"showschema loader=Text{col=A:R4:0 col=B:R4:1 col=C:R4:2} xf=valuemap{data="
                                    + dataFile
                                    + @" col=A:B } in=f:\1.txt" }), (int)0);
        }

        [Fact]
        public void TestCommandLineNoLoaderWithColumnNames()
        {
            var dataFile = GetDataPath("lm.labels.txt");
            Assert.Equal(Maml.Main(new[] { @"showschema loader=Text{col=A:R4:0 col=B:R4:1 col=C:R4:2} xf=valuemap{data="
                                    + dataFile
                                    + @" col=A:B keyCol=foo valueCol=bar} in=f:\1.txt" }), (int)0);
        }

        [Fact]
        public void TestCommandLineNoLoaderWithoutTreatValuesAsKeys()
        {
            var dataFile = GetDataPath("lm.labels.txt");
            Assert.Equal(Maml.Main(new[] { @"showschema loader=Text{col=A:R4:0 col=B:R4:1 col=C:R4:2} xf=valuemap{data="
                                    + dataFile
                                    + @" col=A:B valuesAsKeyType=-} in=f:\1.txt" }), (int)0);
        }

        [Fact]
        public void TestSavingAndLoading()
        {
            var data = new[] { new TestClass() { A = "bar", B = "foo", C = "test", } };
            var dataView = ML.Data.LoadFromEnumerable(data);

            var keyValuePairs = new List<KeyValuePair<string, int>>() {
                new KeyValuePair<string,int>("foo", 2),
                new KeyValuePair<string,int>("bar", 43),
                new KeyValuePair<string,int>("test", 56)};

            var est = ML.Transforms.Conversion.MapValue(keyValuePairs,
                new[] { new InputOutputColumnPair("D", "A"), new InputOutputColumnPair("E", "B") });

            var transformer = est.Fit(dataView);
            using (var ms = new MemoryStream())
            {
                ML.Model.Save(transformer, null, ms);
                ms.Position = 0;
                var loadedTransformer = ML.Model.Load(ms, out var schema);
                var result = loadedTransformer.Transform(dataView);
                Assert.Equal(5, result.Schema.Count);
                Assert.True(result.Schema.TryGetColumnIndex("D", out int col));
                Assert.True(result.Schema.TryGetColumnIndex("E", out col));
            }
        }


        [Fact]
        public void TestValueMapBackCompatTermLookup()
        {
            // Model generated with: xf=drop{col=A} 
            // Expected output: Features Label B C
            var data = new[] { new TestTermLookup() { Label = "good", GroupId = 1 } };
            var dataView = ML.Data.LoadFromEnumerable(data);
            string termLookupModelPath = GetDataPath("backcompat/termlookup.zip");
            using (FileStream fs = File.OpenRead(termLookupModelPath))
            {
                var result = ModelFileUtils.LoadTransforms(Env, dataView, fs);
                Assert.True(result.Schema.TryGetColumnIndex("Features", out int featureIdx));
                Assert.True(result.Schema.TryGetColumnIndex("Label", out int labelIdx));
                Assert.True(result.Schema.TryGetColumnIndex("GroupId", out int groupIdx));
            }
        }

        [Fact]
        public void TestValueMapBackCompatTermLookupKeyTypeValue()
        {
            // Model generated with: xf=drop{col=A} 
            // Expected output: Features Label B C
            var data = new[] { new TestTermLookup() { Label = "Good", GroupId = 1 } };
            var dataView = ML.Data.LoadFromEnumerable(data);
            string termLookupModelPath = GetDataPath("backcompat/termlookup_with_key.zip");
            using (FileStream fs = File.OpenRead(termLookupModelPath))
            {
                var result = ModelFileUtils.LoadTransforms(Env, dataView, fs);
                Assert.True(result.Schema.TryGetColumnIndex("Features", out int featureIdx));
                Assert.True(result.Schema.TryGetColumnIndex("Label", out int labelIdx));
                Assert.True(result.Schema.TryGetColumnIndex("GroupId", out int groupIdx));

                Assert.True(result.Schema[labelIdx].Type is KeyDataViewType);
                Assert.Equal((ulong)5, result.Schema[labelIdx].Type.GetItemType().GetKeyCount());

                var t = result.GetColumn<uint>(result.Schema["Label"]);
                uint s = t.First();
                Assert.Equal((uint)3, s);
            }
        }

        [Fact]
        public void TestValueMapWithNonDefaultColumnOrder()
        {
            // Get a small dataset as an IEnumerable.
            var rawData = new[] {
                new DataPoint() { Price = 3.14f },
                new DataPoint() { Price = 2000f },
                new DataPoint() { Price = 1.19f },
                new DataPoint() { Price = 2.17f },
                new DataPoint() { Price = 33.784f },
            };

            // Convert to IDataView
            var data = ML.Data.LoadFromEnumerable(rawData);

            // Create the lookup map data IEnumerable.   
            var lookupData = new[] {
                new LookupMap { Value = 3.14f, Category = "Low" },
                new LookupMap { Value = 1.19f , Category = "Low" },
                new LookupMap { Value = 2.17f , Category = "Low" },
                new LookupMap { Value = 33.784f, Category = "Medium" },
                new LookupMap { Value = 2000f, Category = "High"}
            };

            // Convert to IDataView
            var lookupIdvMap = ML.Data.LoadFromEnumerable(lookupData);

            // Constructs the ValueMappingEstimator making the ML.NET pipeline
            var pipeline = ML.Transforms.Conversion.MapValue("PriceCategory", lookupIdvMap, lookupIdvMap.Schema["Value"], lookupIdvMap.Schema["Category"], "Price");

            // Fits the ValueMappingEstimator and transforms the data converting the Price to PriceCategory.
            IDataView transformedData = pipeline.Fit(data).Transform(data);

            // Getting the resulting data as an IEnumerable.
            var features = ML.Data.CreateEnumerable<TransformedData>(transformedData, reuseRowObject: false).ToList();

            var expectedCategories = new string[] { "Low", "High", "Low", "Low", "Medium" };

            for (int i = 0; i < features.Count; ++i)
            {
                var feature = features[i];
                Assert.Equal(rawData[i].Price, feature.Price);
                Assert.Equal(expectedCategories[i], feature.PriceCategory);
            }
        }

        private class LookupMap
        {
            public string Category { get; set; }
            public float Value { get; set; }
        }

        private class DataPoint
        {
            public float Price { get; set; }
        }

        private class TransformedData : DataPoint
        {
            public string PriceCategory { get; set; }
        }
    }
}
