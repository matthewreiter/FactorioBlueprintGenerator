using System.Collections.Generic;

namespace CompilerCommon
{
    public class CompiledProgram
    {
        public string Name { get; set; }
        public List<Instruction> Instructions { get; set; }
    }
}
