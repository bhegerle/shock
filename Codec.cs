﻿using System.Security.Cryptography;
using System.Text;
using static System.Security.Cryptography.HMACSHA512;

namespace WebStunnel;

internal enum CodecState {
    Init,
    Handshake,
    Active,
    Error
}

internal class Codec {
    private const int HashSize = 512 / 8;
    private readonly ArraySegment<byte> key, auth, verify, tmp;
    private readonly byte protoByte;

    internal Codec(ProtocolByte protoByte, Config config) {
        if (string.IsNullOrEmpty(config.Key))
            throw new Exception("key required");

        this.protoByte = (byte)protoByte;

        key = SHA512.HashData(Encoding.UTF8.GetBytes(config.Key));

        auth = new byte[HashSize];
        verify = new byte[HashSize];
        tmp = new byte[HashSize];

        State = CodecState.Init;
    }

    internal CodecState State { get; private set; }
    internal static int InitMessageSize => HashSize;

    internal ArraySegment<byte> InitHandshake(ArraySegment<byte> seg) {
        try {
            Transition(CodecState.Init, CodecState.Handshake);

            using var rng = RandomNumberGenerator.Create();

            seg = seg[..InitMessageSize];
            rng.GetBytes(seg);

            return seg;
        } catch {
            SetError();
            throw;
        }
    }

    internal void VerifyHandshake(ArraySegment<byte> seg) {
        try {
            Transition(CodecState.Handshake, CodecState.Active);

            if (seg.Count != InitMessageSize)
                throw new Exception("wrong size for init handshake message");

            var acat = Cat(protoByte, auth, verify);
            var vcat = Cat((byte)~protoByte, verify, auth);

            HashData(key, acat, auth);
            HashData(key, vcat, verify);
        } catch {
            SetError();
            throw;
        }
    }

    internal ArraySegment<byte> AuthMessage(ArraySegment<byte> seg) {
        try {
            CheckState(CodecState.Active);
            return AuthMsg(seg);
        } catch {
            SetError();
            throw;
        }
    }

    internal ArraySegment<byte> VerifyMessage(ArraySegment<byte> seg) {
        try {
            CheckState(CodecState.Active);
            return VerifyMsg(seg);
        } catch {
            SetError();
            throw;
        }
    }

    private ArraySegment<byte> AuthMsg(ArraySegment<byte> seg) {
        var msg = new Frame(seg, HashSize, true);

        auth.CopyTo(msg.Suffix);
        HashData(key, msg.Complete, auth);

        auth.CopyTo(msg.Suffix);

        return msg.Complete;
    }

    private ArraySegment<byte> VerifyMsg(ArraySegment<byte> seg) {
        var msg = new Frame(seg, HashSize, false);

        msg.Suffix.CopyTo(tmp);
        verify.CopyTo(msg.Suffix);
        HashData(key, msg.Complete, verify);

        if (!Utils.ConjEqual(verify, tmp))
            throw new Exception("invalid HMAC");

        return msg.Message;
    }

    private void CheckState(CodecState expected) {
        if (State != expected)
            throw new Exception("invalid codec state");
    }

    private void Transition(CodecState expected, CodecState next) {
        CheckState(expected);
        State = next;
    }

    private void SetError() {
        State = CodecState.Error;
    }

    private static byte[] Cat(byte b, ArraySegment<byte> seg0, ArraySegment<byte> seg1) {
        var cat = new byte[1 + seg0.Count + seg1.Count];
        cat[0] = b;
        seg0.CopyTo(cat, 1);
        seg1.CopyTo(cat, seg0.Count + 1);
        return cat;
    }
}

internal readonly struct Frame {
    internal Frame(ArraySegment<byte> x, int suffixSize, bool extend) {
        SuffixSize = suffixSize;

        if (extend)
            x = x.Extend(suffixSize);

        Complete = x;
    }

    internal int SuffixSize { get; }
    internal ArraySegment<byte> Complete { get; }

    internal ArraySegment<byte> Message => Complete[..^SuffixSize];
    internal ArraySegment<byte> Suffix => Complete[^SuffixSize..];
}