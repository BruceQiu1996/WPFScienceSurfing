using Shadowsocks.Encryption.Exception;
using System;
using System.Collections.Generic;

namespace Shadowsocks.Encryption.Stream
{
    public class StreamSodiumEncryptor : StreamEncryptor
    {
        private const int CIPHER_SALSA20 = 1;
        private const int CIPHER_CHACHA20 = 2;
        private const int CIPHER_CHACHA20_IETF = 3;
        private const int CIPHER_XSALSA20 = 4 + 1;
        private const int CIPHER_XCHACHA20 = 4 + 2;

        private const int SODIUM_BLOCK_SIZE = 64;

        protected int _encryptBytesRemaining;
        protected int _decryptBytesRemaining;
        protected ulong _encryptIC;
        protected ulong _decryptIC;
        protected byte[] _encryptBuf;
        protected byte[] _decryptBuf;

        private delegate int CryptoStream(byte[] c, byte[] m, ulong mlen, byte[] n, ulong ic, byte[] k);
        private readonly CryptoStream _encryptorDelegate;

        public StreamSodiumEncryptor(string method, string password) : base(method, password)
        {
            _encryptBuf = new byte[MAX_INPUT_SIZE + SODIUM_BLOCK_SIZE];
            _decryptBuf = new byte[MAX_INPUT_SIZE + SODIUM_BLOCK_SIZE];
            _encryptorDelegate = _cipher switch
            {
                CIPHER_SALSA20 => Sodium.crypto_stream_salsa20_xor_ic,
                CIPHER_CHACHA20 => Sodium.crypto_stream_chacha20_xor_ic,
                CIPHER_XSALSA20 => Sodium.crypto_stream_xsalsa20_xor_ic,
                CIPHER_XCHACHA20 => Sodium.crypto_stream_xchacha20_xor_ic,
                CIPHER_CHACHA20_IETF => crypto_stream_chacha20_ietf_xor_ic,
                _ => _encryptorDelegate
            };
        }

        private static Dictionary<string, EncryptorInfo> _ciphers = new()
        {
            { @"salsa20", new EncryptorInfo(32, 8, CIPHER_SALSA20) },
            { @"chacha20", new EncryptorInfo(32, 8, CIPHER_CHACHA20) },
            { @"xsalsa20", new EncryptorInfo(32, 24, CIPHER_XSALSA20) },
            { @"xchacha20", new EncryptorInfo(32, 24, CIPHER_XCHACHA20) },
            { @"chacha20-ietf", new EncryptorInfo(32, 12, CIPHER_CHACHA20_IETF) }
        };

        protected override Dictionary<string, EncryptorInfo> getCiphers()
        {
            return _ciphers;
        }

        public static List<string> SupportedCiphers()
        {
            return new(_ciphers.Keys);
        }

        protected override void CipherUpdate(bool isCipher, int length, byte[] buf, byte[] outbuf)
        {
            int bytesRemaining;
            ulong ic;
            byte[] sodiumBuf;
            byte[] iv;
            if (isCipher)
            {
                bytesRemaining = _encryptBytesRemaining;
                ic = _encryptIC;
                sodiumBuf = _encryptBuf;
                iv = _encryptIV;
            }
            else
            {
                bytesRemaining = _decryptBytesRemaining;
                ic = _decryptIC;
                sodiumBuf = _decryptBuf;
                iv = _decryptIV;
            }
            var padding = bytesRemaining;
            Buffer.BlockCopy(buf, 0, sodiumBuf, padding, length);
            var ret = _encryptorDelegate(sodiumBuf, sodiumBuf, (ulong)(padding + length), iv, ic, _key);
            if (ret != 0)
            {
                throw new CryptoErrorException();
            }

            Buffer.BlockCopy(sodiumBuf, padding, outbuf, 0, length);
            padding += length;
            ic += (ulong)padding / SODIUM_BLOCK_SIZE;
            bytesRemaining = padding % SODIUM_BLOCK_SIZE;

            if (isCipher)
            {
                _encryptBytesRemaining = bytesRemaining;
                _encryptIC = ic;
            }
            else
            {
                _decryptBytesRemaining = bytesRemaining;
                _decryptIC = ic;
            }
        }

        public override void ResetEncrypt()
        {
            _encryptIVSent = false;
            _encryptIC = 0;
            _encryptBytesRemaining = 0;
        }

        public override void ResetDecrypt()
        {
            _decryptIVReceived = false;
            _decryptIC = 0;
            _decryptBytesRemaining = 0;
        }

        private int crypto_stream_chacha20_ietf_xor_ic(byte[] c, byte[] m, ulong mlen, byte[] n, ulong ic, byte[] k)
        {
            return Sodium.crypto_stream_chacha20_ietf_xor_ic(c, m, mlen, n, (uint)ic, k);
        }

        public override void Dispose()
        {
        }
    }
}
