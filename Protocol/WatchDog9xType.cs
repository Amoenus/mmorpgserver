//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from: WatchDog9xType.proto
// Note: requires additional types generated from: CommonData.proto
// Note: requires additional types generated from: MessageData.proto
namespace DataContract
{
  [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"__RPC_WatchDog_Test_RET_int32__")]
  public partial class __RPC_WatchDog_Test_RET_int32__ : global::ProtoBuf.IExtensible
  {
    public __RPC_WatchDog_Test_RET_int32__() {}
    

    private int _ReturnValue = default(int);
    [global::ProtoBuf.ProtoMember(1, IsRequired = false, Name=@"ReturnValue", DataFormat = global::ProtoBuf.DataFormat.TwosComplement)]
    [global::System.ComponentModel.DefaultValue(default(int))]
    public int ReturnValue
    {
      get { return _ReturnValue; }
      set { _ReturnValue = value; }
    }
    private global::ProtoBuf.IExtension extensionObject;
    global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
      { return global::ProtoBuf.Extensible.GetExtensionObject(ref extensionObject, createIfMissing); }
  }
  
  [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"__RPC_WatchDog_Test_ARG_uint64_characterId__")]
  public partial class __RPC_WatchDog_Test_ARG_uint64_characterId__ : global::ProtoBuf.IExtensible
  {
    public __RPC_WatchDog_Test_ARG_uint64_characterId__() {}
    

    private ulong _CharacterId = default(ulong);
    [global::ProtoBuf.ProtoMember(1, IsRequired = false, Name=@"CharacterId", DataFormat = global::ProtoBuf.DataFormat.TwosComplement)]
    [global::System.ComponentModel.DefaultValue(default(ulong))]
    public ulong CharacterId
    {
      get { return _CharacterId; }
      set { _CharacterId = value; }
    }
    private global::ProtoBuf.IExtension extensionObject;
    global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
      { return global::ProtoBuf.Extensible.GetExtensionObject(ref extensionObject, createIfMissing); }
  }
  
}