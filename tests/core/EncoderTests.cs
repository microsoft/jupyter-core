using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{

    [TestClass]
    public class EncoderTests
    {
        private readonly ITable exampleTable =
            new Table<(int, double)>
            {
                Columns = new List<(string, Func<(int, double), string>)>
                {
                    ("foo", row => row.Item1.ToString()),
                    ("bar", row => row.Item2.ToString())
                },

                Rows = new List<(int, double)>
                {
                    (42, 3.14),
                    (1337, 2.718)
                }
            };

        [TestMethod]
        public void TestStringToPlainText()
        {
            var encoder = new PlainTextResultEncoder();
            Assert.AreEqual(encoder.MimeType, MimeTypes.PlainText);
            var data = encoder.Encode("foo");
            Assert.IsTrue(data.HasValue);
            Assert.AreEqual(data.Value.Data, "foo");
            Assert.AreEqual(data.Value.Metadata, null);

            data = encoder.Encode(null);
            Assert.IsTrue(data.HasValue);
            Assert.AreEqual(data.Value.Data, null);
            Assert.AreEqual(data.Value.Metadata, null);
        }


        [TestMethod]
        public void TestListToPlainText()
        {
            var encoder = new ListToTextResultEncoder();
            Assert.AreEqual(encoder.MimeType, MimeTypes.PlainText);
            Assert.IsNull(encoder.Encode(null));
            var data = encoder.Encode(new [] {"foo", "bar"});
            Assert.IsTrue(data.HasValue);
            Assert.AreEqual(data.Value.Data, "foo, bar");
            Assert.AreEqual(data.Value.Metadata, null);
        }

        [TestMethod]
        public void TestListToHtml()
        {
            var encoder = new ListToHtmlResultEncoder();
            Assert.AreEqual(encoder.MimeType, MimeTypes.Html);
            Assert.IsNull(encoder.Encode(null));
            var data = encoder.Encode(new [] {"foo", "bar"});
            Assert.IsTrue(data.HasValue);
            Assert.AreEqual(data.Value.Data, "<ul><li>foo</li><li>bar</li></ul>");
            Assert.AreEqual(data.Value.Metadata, null);
        }

        [TestMethod]
        public void TestFuncEncoder()
        {
            var encoder = new FuncResultEncoder(
                MimeTypes.PlainText,
                displayable => String.Join("", displayable.ToString().Reverse())
            );
            Assert.AreEqual(encoder.MimeType, MimeTypes.PlainText);
            Assert.IsNull(encoder.Encode(null));
            var data = encoder.Encode("foo");
            Assert.IsTrue(data.HasValue);
            Assert.AreEqual(data.Value.Data, "oof");
            Assert.AreEqual(data.Value.Metadata, null);
        }

        [TestMethod]
        public void TestTableToText()
        {
            var encoder = new TableToTextDisplayEncoder();
            Assert.AreEqual(encoder.MimeType, MimeTypes.PlainText);
            Assert.IsNull(encoder.Encode(null));
            var data = encoder.Encode(exampleTable);
            Assert.IsTrue(data.HasValue);
            var expected = @"foo  bar
---- -----
42   3.14
1337 2.718
";
            Assert.AreEqual(data.Value.Data, expected);
            Assert.AreEqual(data.Value.Metadata, null);
        }

        [TestMethod]
        public void TestTableToHtml()
        {
            var encoder = new TableToHtmlDisplayEncoder();
            Assert.AreEqual(encoder.MimeType, MimeTypes.Html);
            Assert.IsNull(encoder.Encode(null));
            var data = encoder.Encode(exampleTable);
            Assert.IsTrue(data.HasValue);
            var expected =
                "<table>" +
                    "<thead><tr><th>foo</th><th>bar</th></tr></thead>" +
                    "<tbody>" +
                        "<tr><td>42</td><td>3.14</td></tr>" +
                        "<tr><td>1337</td><td>2.718</td></tr>" +
                    "</tbody>" +
                "</table>";
            Assert.AreEqual(data.Value.Data, expected);
            Assert.AreEqual(data.Value.Metadata, null);
        }

        [TestMethod]
        public void TestJsonEncoder()
        {
            var encoder = new JsonResultEncoder(
                new ConsoleLogger(
                    "test",
                    (msg, level) => true,
                    true
                )
            );
            Assert.AreEqual(encoder.MimeType, MimeTypes.Json);
            Assert.IsNull(encoder.Encode(null));
            var data = encoder.Encode(exampleTable);
            var jData = JObject.Parse(data.Value.Data);
            Assert.IsTrue(JToken.DeepEquals(
                jData,
                new JObject
                {
                    {"rows", new JArray
                        {
                            new JObject { {"Item1", 42}, {"Item2", 3.14} },
                            new JObject { {"Item1", 1337}, {"Item2", 2.718} },
                        }
                    }
                }
            ));
        }

        [TestMethod]
        public void TestSymbolIcons()
        {
            var encoder = new MagicSymbolToHtmlResultEncoder();
            Assert.IsNull(encoder.Encode(null));

            foreach (SymbolKind kind in Enum.GetValues(typeof(SymbolKind)))
            {
                Assert.IsTrue(encoder.Icons.ContainsKey(kind));
            }
        }
    }
}
