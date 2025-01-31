﻿using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Faultify.TestRunner.Shared
{
    /// <summary>
    ///     Test results from a test session.
    /// </summary>
    // TODO: Uses custom format because Json requires external package.
    // External packages are somehow not working with test data collectors.
    public class TestResults
    {
        /// <summary>
        ///     A list of the test result from each test in the session.
        /// </summary>
        public List<TestResult> Tests { get; set; } = new List<TestResult>();

        public byte[] Serialize()
        {
            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(Tests.Count);
            foreach (var testResult in Tests)
            {
                binaryWriter.Write(testResult.Name);
                binaryWriter.Write((int)testResult.Outcome);
                binaryWriter.Write(testResult.Guid.ToByteArray());
            }

            return memoryStream.ToArray();
        }

        public static TestResults Deserialize(byte[] data, bool trimNames = false)
        {
            var testResults = new TestResults();
            var memoryStream = new MemoryStream(data);
            var binaryReader = new BinaryReader(memoryStream);
            var count = binaryReader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var testResult = new TestResult();
                var name = binaryReader.ReadString();
                var testOutcome = (TestOutcome)binaryReader.ReadInt32(); 
                var guidBytes = new byte[16];
                binaryReader.Read(guidBytes, 0, guidBytes.Length);

                testResult.Name = trimNames ? name.Split('(')[0] : name;
                testResult.Outcome = testOutcome;
                testResult.Guid = new Guid(guidBytes);
                testResults.Tests.Add(testResult);
            }

            return testResults;
        }
    }
}