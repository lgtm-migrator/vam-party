using Moq;
using NUnit.Framework;
using Party.Shared;
using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Party.CLI
{
    public abstract class CommandTestsBase
    {
        protected Mock<IRenderer> _renderer;
        protected Mock<IPartyController> _controller;
        protected Program _program;
        protected StringBuilder _out;

        [SetUp]
        public void CreateDependencies()
        {
            _out = new StringBuilder();
            var outWriter = StandardStreamWriter.Create(new StringWriter(_out));
            _renderer = new Mock<IRenderer>(MockBehavior.Strict);
            _renderer.Setup(x => x.WriteLine(It.IsAny<string>())).Callback((string line) => _out.Append($"{line}\n"));
            _renderer.Setup(x => x.WhenCompleteAsync()).Returns(Task.CompletedTask);
            _renderer.Setup(x => x.WithColor(It.IsAny<ConsoleColor>())).Returns((ConsoleColor color) => new ColorStub(_out, color));
            _renderer.Setup(x => x.Out).Returns(outWriter);
            _renderer.Setup(x => x.Error).Returns(outWriter);
            _controller = new Mock<IPartyController>(MockBehavior.Strict);
            var config = PartyConfigurationFactory.Create(@"C:\VaM");
            _program = new Program(_renderer.Object, config, _controller.Object);
        }

        protected string[] GetOutput()
        {
            return _out.ToString().Trim().Split(new[] { '\r', '\n' });
        }

        private class ColorStub : IDisposable
        {
            private readonly StringBuilder _out;

            public ColorStub(StringBuilder output, ConsoleColor color)
            {
                _out = output;
                _out.Append($"[color:{color}]");
            }

            public void Dispose()
            {
                _out.Append("[/color]");
            }
        }
    }
}