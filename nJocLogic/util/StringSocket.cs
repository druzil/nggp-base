using System.IO;
using System.Text;

namespace nJocLogic.util
{
    public class StringSocket : ISocketWrapper
    {
        Stream _input;
        TextWriter _output;

        public StringSocket(string input, TextWriter output)
        {
            //stream = new MemoryStream(System.Text.Encoding.Default.GetBytes(input));
            _input = new MemoryStream();
            byte[] bytes = Encoding.Default.GetBytes(input);
            _input.Write(bytes, 0, bytes.Length);
            _input.Position = 0;

            _output = output;
        }

        public void Close()
        {
            _input = null;
            // Don't close the output stream! Just make it null.
            _output = null;
        }

        public Stream GetStream()
        {
            return _input;
        }

        public TextWriter GetWriter()
        {
            return _output;
        }
    }

}
