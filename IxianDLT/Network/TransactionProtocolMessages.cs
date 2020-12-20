﻿using DLT.Meta;
using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DLT
{
    namespace Network
    {
        public class TransactionProtocolMessages
        {
            // Handle the getBlockTransactions message
            // This is called from NetworkProtocol
            public static void handleGetBlockTransactions(ulong blockNum, bool requestAllTransactions, RemoteEndpoint endpoint)
            {
                //Logging.info(String.Format("Received request for transactions in block {0}.", blockNum));

                // Get the requested block and corresponding transactions
                bool applied_block = true;
                Block b = Node.blockChain.getBlock(blockNum, Config.storeFullHistory);
                List<byte[]> txIdArr = null;
                if (b != null)
                {
                    txIdArr = new List<byte[]>(b.transactions);
                }
                else
                {
                    // Block is likely local, fetch the transactions

                    bool haveLock = false;
                    try
                    {
                        Monitor.TryEnter(Node.blockProcessor.localBlockLock, 1000, ref haveLock);
                        if (!haveLock)
                        {
                            throw new TimeoutException();
                        }

                        Block tmp = Node.blockProcessor.getLocalBlock();
                        if (tmp != null && tmp.blockNum == blockNum)
                        {
                            applied_block = false;
                            b = tmp;
                            txIdArr = new List<byte[]>(tmp.transactions);
                        }
                    }
                    finally
                    {
                        if (haveLock)
                        {
                            Monitor.Exit(Node.blockProcessor.localBlockLock);
                        }
                    }
                }

                if (txIdArr == null)
                    return;

                int tx_count = txIdArr.Count();

                if (tx_count == 0)
                    return;

                // Go through each chunk
                for (int i = 0; i < tx_count;)
                {
                    using (MemoryStream mOut = new MemoryStream(4096))
                    {
                        int txs_in_chunk = 0;
                        using (BinaryWriter writer = new BinaryWriter(mOut))
                        {
                            // Generate a chunk of transactions
                            for (int j = 0; j < CoreConfig.maximumTransactionsPerChunk && i < tx_count; j++)
                            {
                                if (!requestAllTransactions)
                                {
                                    if (txIdArr[i][0] == 0) // stk
                                    {
                                        i++;
                                        continue;
                                    }
                                }
                                Transaction tx;
                                if(applied_block)
                                {
                                    tx = TransactionPool.getAppliedTransaction(txIdArr[i], blockNum, true);
                                }else
                                {
                                    tx = TransactionPool.getUnappliedTransaction(txIdArr[i]);
                                }
                                i++;
                                if (tx != null)
                                {
                                    byte[] txBytes = tx.getBytes();

                                    long rollback_len = mOut.Length;
                                    writer.Write(txBytes.Length);
                                    writer.Write(txBytes);
                                    if (mOut.Length > CoreConfig.maxMessageSize)
                                    {
                                        mOut.SetLength(rollback_len);
                                        i--;
                                        break;
                                    }
                                    txs_in_chunk++;
                                }
                            }

#if TRACE_MEMSTREAM_SIZES
                            Logging.info(String.Format("NetworkProtocol::handleGetBlockTransactions: {0}", mOut.Length));
#endif
                        }
                        if (txs_in_chunk > 0)
                        {
                            // Send a chunk
                            endpoint.sendData(ProtocolMessageCode.blockTransactionsChunk, mOut.ToArray());
                        }
                    }
                }
            }

            // Handle the getBlockTransactions message
            // This is called from NetworkProtocol
            public static void handleGetBlockTransactions2(ulong blockNum, bool requestAllTransactions, RemoteEndpoint endpoint)
            {
                //Logging.info(String.Format("Received request for transactions in block {0}.", blockNum));

                // Get the requested block and corresponding transactions
                bool applied_block = true;
                Block b = Node.blockChain.getBlock(blockNum, Config.storeFullHistory);
                List<byte[]> txIdArr = null;
                if (b != null)
                {
                    txIdArr = new List<byte[]>(b.transactions);
                }
                else
                {
                    // Block is likely local, fetch the transactions

                    bool haveLock = false;
                    try
                    {
                        Monitor.TryEnter(Node.blockProcessor.localBlockLock, 1000, ref haveLock);
                        if (!haveLock)
                        {
                            throw new TimeoutException();
                        }

                        Block tmp = Node.blockProcessor.getLocalBlock();
                        if (tmp != null && tmp.blockNum == blockNum)
                        {
                            applied_block = false;
                            b = tmp;
                            txIdArr = new List<byte[]>(tmp.transactions);
                        }
                    }
                    finally
                    {
                        if (haveLock)
                        {
                            Monitor.Exit(Node.blockProcessor.localBlockLock);
                        }
                    }
                }

                if (txIdArr == null)
                    return;

                int tx_count = txIdArr.Count();

                if (tx_count == 0)
                    return;

                // Go through each chunk
                for (int i = 0; i < tx_count;)
                {
                    using (MemoryStream mOut = new MemoryStream(4096))
                    {
                        int txs_in_chunk = 0;
                        using (BinaryWriter writer = new BinaryWriter(mOut))
                        {
                            writer.WriteIxiVarInt(tx_count);
                            // Generate a chunk of transactions
                            for (int j = 0; j < CoreConfig.maximumTransactionsPerChunk && i < tx_count; j++)
                            {
                                if (!requestAllTransactions)
                                {
                                    if (txIdArr[i][0] == 0) // stk
                                    {
                                        i++;
                                        continue;
                                    }
                                }
                                Transaction tx;
                                if(applied_block)
                                {
                                    tx = TransactionPool.getAppliedTransaction(txIdArr[i], blockNum, true);
                                }else
                                {
                                    tx = TransactionPool.getUnappliedTransaction(txIdArr[i]);
                                }
                                i++;
                                if (tx != null)
                                {
                                    byte[] txBytes = tx.getBytes();

                                    long rollback_len = mOut.Length;
                                    writer.WriteIxiVarInt(txBytes.Length);
                                    writer.Write(txBytes);
                                    if (mOut.Length > CoreConfig.maxMessageSize)
                                    {
                                        mOut.SetLength(rollback_len);
                                        i--;
                                        break;
                                    }
                                    txs_in_chunk++;
                                }
                            }

#if TRACE_MEMSTREAM_SIZES
                            Logging.info(String.Format("NetworkProtocol::handleGetBlockTransactions: {0}", mOut.Length));
#endif
                        }
                        if (txs_in_chunk > 0)
                        {
                            // Send a chunk
                            endpoint.sendData(ProtocolMessageCode.transactionsChunk, mOut.ToArray(), null);
                        }
                    }
                }
            }

            // Handle the getBlockTransactions message
            // This is called from NetworkProtocol
            public static void handleGetBlockTransactions3(ulong blockNum, bool requestAllTransactions, RemoteEndpoint endpoint)
            {
                //Logging.info(String.Format("Received request for transactions in block {0}.", blockNum));

                // Get the requested block and corresponding transactions
                bool applied_block = true;
                Block b = Node.blockChain.getBlock(blockNum, Config.storeFullHistory);
                List<byte[]> txIdArr = null;
                if (b != null)
                {
                    txIdArr = new List<byte[]>(b.transactions);
                }
                else
                {
                    // Block is likely local, fetch the transactions

                    bool haveLock = false;
                    try
                    {
                        Monitor.TryEnter(Node.blockProcessor.localBlockLock, 1000, ref haveLock);
                        if (!haveLock)
                        {
                            throw new TimeoutException();
                        }

                        Block tmp = Node.blockProcessor.getLocalBlock();
                        if (tmp != null && tmp.blockNum == blockNum)
                        {
                            applied_block = false;
                            b = tmp;
                            txIdArr = new List<byte[]>(tmp.transactions);
                        }
                    }
                    finally
                    {
                        if (haveLock)
                        {
                            Monitor.Exit(Node.blockProcessor.localBlockLock);
                        }
                    }
                }

                if (txIdArr == null)
                    return;

                int tx_count = txIdArr.Count();

                if (tx_count == 0)
                    return;

                long msg_id = -(long)blockNum;

                // Go through each chunk
                for (int i = 0; i < tx_count;)
                {
                    using (MemoryStream mOut = new MemoryStream(4096))
                    {
                        int txs_in_chunk = 0;
                        using (BinaryWriter writer = new BinaryWriter(mOut))
                        {
                            writer.WriteIxiVarInt(msg_id);
                            writer.WriteIxiVarInt(tx_count);
                            // Generate a chunk of transactions
                            for (int j = 0; j < CoreConfig.maximumTransactionsPerChunk && i < tx_count; j++)
                            {
                                if (!requestAllTransactions)
                                {
                                    if (txIdArr[i][0] == 0) // stk
                                    {
                                        i++;
                                        continue;
                                    }
                                }
                                Transaction tx;
                                if (applied_block)
                                {
                                    tx = TransactionPool.getAppliedTransaction(txIdArr[i], blockNum, true);
                                }
                                else
                                {
                                    tx = TransactionPool.getUnappliedTransaction(txIdArr[i]);
                                }
                                i++;
                                if (tx != null)
                                {
                                    byte[] txBytes = tx.getBytes();

                                    long rollback_len = mOut.Length;
                                    writer.WriteIxiVarInt(txBytes.Length);
                                    writer.Write(txBytes);
                                    if (mOut.Length > CoreConfig.maxMessageSize)
                                    {
                                        mOut.SetLength(rollback_len);
                                        i--;
                                        break;
                                    }
                                    txs_in_chunk++;
                                }
                            }

#if TRACE_MEMSTREAM_SIZES
                            Logging.info(String.Format("NetworkProtocol::handleGetBlockTransactions: {0}", mOut.Length));
#endif
                        }
                        if (txs_in_chunk > 0)
                        {
                            // Send a chunk
                            endpoint.sendData(ProtocolMessageCode.transactionsChunk2, mOut.ToArray(), null, 0, MessagePriority.high);
                        }
                    }
                }
            }

            public static void broadcastGetTransactions(List<byte[]> tx_list, RemoteEndpoint endpoint)
            {
                int tx_count = tx_list.Count;
                int max_tx_per_chunk = CoreConfig.maximumTransactionsPerChunk;
                for (int i = 0; i < tx_count;)
                {
                    using (MemoryStream mOut = new MemoryStream(max_tx_per_chunk * 570))
                    {
                        using (BinaryWriter writer = new BinaryWriter(mOut))
                        {
                            int next_tx_count = tx_count - i;
                            if (next_tx_count > max_tx_per_chunk)
                            {
                                next_tx_count = max_tx_per_chunk;
                            }
                            writer.WriteIxiVarInt(next_tx_count);

                            for (int j = 0; j < next_tx_count && i < tx_count; j++)
                            {
                                long rollback_len = mOut.Length;

                                byte[] txid = UTF8Encoding.UTF8.GetBytes(Transaction.txIdV8ToLegacy(tx_list[i]));
                                i++;
                                writer.WriteIxiVarInt(txid.Length);
                                writer.Write(txid);

                                if (mOut.Length > CoreConfig.maxMessageSize)
                                {
                                    mOut.SetLength(rollback_len);
                                    i--;
                                    break;
                                }
                            }
                        }
                        endpoint.sendData(ProtocolMessageCode.getTransactions, mOut.ToArray(), null);
                    }
                }
            }

            public static void broadcastGetTransactions2(List<byte[]> tx_list, long msg_id, RemoteEndpoint endpoint)
            {
                int tx_count = tx_list.Count;
                int max_tx_per_chunk = CoreConfig.maximumTransactionsPerChunk;
                for (int i = 0; i < tx_count;)
                {
                    using (MemoryStream mOut = new MemoryStream(max_tx_per_chunk * 570))
                    {
                        using (BinaryWriter writer = new BinaryWriter(mOut))
                        {
                            int next_tx_count = tx_count - i;
                            if (next_tx_count > max_tx_per_chunk)
                            {
                                next_tx_count = max_tx_per_chunk;
                            }
                            writer.WriteIxiVarInt(msg_id);
                            writer.WriteIxiVarInt(next_tx_count);

                            for (int j = 0; j < next_tx_count && i < tx_count; j++)
                            {
                                long rollback_len = mOut.Length;

                                writer.WriteIxiVarInt(tx_list[i].Length);
                                writer.Write(tx_list[i]);

                                i++;

                                if (mOut.Length > CoreConfig.maxMessageSize)
                                {
                                    mOut.SetLength(rollback_len);
                                    i--;
                                    break;
                                }
                            }
                        }
                        MessagePriority priority = msg_id > 0 ? MessagePriority.high : MessagePriority.auto;
                        endpoint.sendData(ProtocolMessageCode.getTransactions2, mOut.ToArray(), null, msg_id, priority);
                    }
                }
            }

            public static void handleGetTransactions(byte[] data, RemoteEndpoint endpoint)
            {
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        int tx_count = (int)reader.ReadIxiVarUInt();

                        int max_tx_per_chunk = CoreConfig.maximumTransactionsPerChunk;
                        if (tx_count > max_tx_per_chunk)
                        {
                            tx_count = max_tx_per_chunk;
                        }

                        for (int i = 0; i < tx_count;)
                        {
                            using (MemoryStream mOut = new MemoryStream(max_tx_per_chunk * 570))
                            {
                                using (BinaryWriter writer = new BinaryWriter(mOut))
                                {
                                    int next_tx_count;
                                    if (tx_count - i > max_tx_per_chunk)
                                    {
                                        next_tx_count = max_tx_per_chunk;
                                    }
                                    else
                                    {
                                        next_tx_count = tx_count - i;
                                    }
                                    writer.WriteIxiVarInt(next_tx_count);

                                    for (int j = 0; j < next_tx_count && i < tx_count; j++)
                                    {
                                        long in_rollback_pos = reader.BaseStream.Position;
                                        long out_rollback_len = mOut.Length;

                                        i++;

                                        if (m.Position == m.Length)
                                        {
                                            break;
                                        }

                                        int txid_len = (int)reader.ReadIxiVarUInt();
                                        byte[] txid = reader.ReadBytes(txid_len);
                                        string txid_str = UTF8Encoding.UTF8.GetString(txid);

                                        Transaction tx = TransactionPool.getUnappliedTransaction(Transaction.txIdLegacyToV8(txid_str));
                                        if (tx == null)
                                        {
                                            tx = TransactionPool.getAppliedTransaction(Transaction.txIdLegacyToV8(txid_str));
                                            if (tx == null)
                                            {
                                                Logging.warn("handleGetTransactions: I do not have txid '{0}.", txid_str);
                                                continue;
                                            }
                                        }

                                        byte[] tx_bytes = tx.getBytes();
                                        byte[] tx_len = IxiVarInt.GetIxiVarIntBytes(tx_bytes.Length);
                                        writer.Write(tx_len);
                                        writer.Write(tx_bytes);

                                        if (mOut.Length > CoreConfig.maxMessageSize)
                                        {
                                            reader.BaseStream.Position = in_rollback_pos;
                                            mOut.SetLength(out_rollback_len);
                                            i--;
                                            break;
                                        }
                                    }
                                }
                                endpoint.sendData(ProtocolMessageCode.transactionsChunk, mOut.ToArray(), null);
                            }
                        }
                    }
                }
            }


            public static void handleGetTransactions2(byte[] data, RemoteEndpoint endpoint)
            {
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        int msg_id = (int)reader.ReadIxiVarInt();
                        int tx_count = (int)reader.ReadIxiVarUInt();

                        int max_tx_per_chunk = CoreConfig.maximumTransactionsPerChunk;
                        if (tx_count > max_tx_per_chunk)
                        {
                            tx_count = max_tx_per_chunk;
                        }

                        for (int i = 0; i < tx_count;)
                        {
                            using (MemoryStream mOut = new MemoryStream(max_tx_per_chunk * 570))
                            {
                                using (BinaryWriter writer = new BinaryWriter(mOut))
                                {
                                    int next_tx_count;
                                    if (tx_count - i > max_tx_per_chunk)
                                    {
                                        next_tx_count = max_tx_per_chunk;
                                    }
                                    else
                                    {
                                        next_tx_count = tx_count - i;
                                    }
                                    writer.WriteIxiVarInt(msg_id);
                                    writer.WriteIxiVarInt(next_tx_count);

                                    for (int j = 0; j < next_tx_count && i < tx_count;  j++)
                                    {
                                        long in_rollback_pos = reader.BaseStream.Position;
                                        long out_rollback_len = mOut.Length;

                                        i++;

                                        if (m.Position == m.Length)
                                        {
                                            break;
                                        }

                                        int txid_len = (int)reader.ReadIxiVarUInt();
                                        byte[] txid = reader.ReadBytes(txid_len);

                                        Transaction tx = TransactionPool.getUnappliedTransaction(txid);
                                        if (tx == null)
                                        {
                                            tx = TransactionPool.getAppliedTransaction(txid);
                                            if (tx == null)
                                            {
                                                Logging.warn("I do not have txid '{0}.", Transaction.txIdV8ToLegacy(txid)); // convert to string
                                                continue;
                                            }
                                        }

                                        byte[] tx_bytes = tx.getBytes();
                                        byte[] tx_len = IxiVarInt.GetIxiVarIntBytes(tx_bytes.Length);
                                        writer.Write(tx_len);
                                        writer.Write(tx_bytes);

                                        if (mOut.Length > CoreConfig.maxMessageSize)
                                        {
                                            reader.BaseStream.Position = in_rollback_pos;
                                            mOut.SetLength(out_rollback_len);
                                            i--;
                                            break;
                                        }
                                    }
                                }
                                endpoint.sendData(ProtocolMessageCode.transactionsChunk2, mOut.ToArray(), null, 0, MessagePriority.high);
                            }
                        }
                    }
                }
            }
            public static void handleTransactionsChunk(byte[] data, RemoteEndpoint endpoint)
            {
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        int tx_count = (int)reader.ReadIxiVarUInt();

                        int max_tx_per_chunk = CoreConfig.maximumTransactionsPerChunk;
                        if (tx_count > max_tx_per_chunk)
                        {
                            tx_count = max_tx_per_chunk;
                        }

                        var sw = new System.Diagnostics.Stopwatch();
                        sw.Start();
                        int processedTxCount = 0;
                        int totalTxCount = 0;
                        for (int i = 0; i < tx_count; i++)
                        {
                            if (m.Position == m.Length)
                            {
                                break;
                            }

                            int tx_len = (int)reader.ReadIxiVarUInt();
                            byte[] tx_bytes = reader.ReadBytes(tx_len);

                            Transaction tx = new Transaction(tx_bytes);

                            totalTxCount++;
                            if (tx.type == (int)Transaction.Type.StakingReward && !Node.blockSync.synchronizing)
                            {
                                continue;
                            }
                            if (TransactionPool.addTransaction(tx, false, endpoint))
                            {
                                processedTxCount++;
                            }
                        }
                        sw.Stop();
                        TimeSpan elapsed = sw.Elapsed;
                        Logging.info("Processed {0}/{1} txs in {2}ms", processedTxCount, totalTxCount, elapsed.TotalMilliseconds);
                    }
                }
            }

            public static void handleTransactionsChunk2(byte[] data, RemoteEndpoint endpoint)
            {
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        long msg_id = reader.ReadIxiVarInt();
                        int tx_count = (int)reader.ReadIxiVarUInt();

                        int max_tx_per_chunk = CoreConfig.maximumTransactionsPerChunk;
                        if (tx_count > max_tx_per_chunk)
                        {
                            tx_count = max_tx_per_chunk;
                        }

                        var sw = new System.Diagnostics.Stopwatch();
                        sw.Start();
                        int processedTxCount = 0;
                        int totalTxCount = 0;
                        for (int i = 0; i < tx_count; i++)
                        {
                            if (m.Position == m.Length)
                            {
                                break;
                            }

                            int tx_len = (int)reader.ReadIxiVarUInt();
                            byte[] tx_bytes = reader.ReadBytes(tx_len);

                            Transaction tx = new Transaction(tx_bytes);

                            totalTxCount++;
                            if (tx.type == (int)Transaction.Type.StakingReward && !Node.blockSync.synchronizing)
                            {
                                continue;
                            }
                            if (TransactionPool.addTransaction(tx, false, endpoint))
                            {
                                processedTxCount++;
                            }
                        }
                        sw.Stop();
                        TimeSpan elapsed = sw.Elapsed;
                        Logging.info("Processed {0}/{1} txs in {2}ms", processedTxCount, totalTxCount, elapsed.TotalMilliseconds);
                    }
                }
            }

            // Handle the getUnappliedTransactions message
            // This is called from NetworkProtocol
            public static void handleGetUnappliedTransactions(byte[] data, RemoteEndpoint endpoint)
            {
                Transaction[] txIdArr = TransactionPool.getUnappliedTransactions();
                int tx_count = txIdArr.Count();

                if (tx_count == 0)
                    return;

                // Go through each chunk
                for (int i = 0; i < tx_count;)
                {
                    using (MemoryStream mOut = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(mOut))
                        {
                            // Generate a chunk of transactions
                            for (int j = 0; j < CoreConfig.maximumTransactionsPerChunk && i < tx_count; j++)
                            {
                                byte[] txBytes = txIdArr[i].getBytes();

                                i++;

                                long rollback_len = mOut.Length;

                                writer.Write(txBytes.Length);
                                writer.Write(txBytes);

                                if (mOut.Length > CoreConfig.maxMessageSize)
                                {
                                    mOut.SetLength(rollback_len);
                                    i--;
                                    break;
                                }
                            }

                            // Send a chunk
#if TRACE_MEMSTREAM_SIZES
                        Logging.info(String.Format("NetworkProtocol::handleGetUnappliedTransactions: {0}", mOut.Length));
#endif
                        }
                        endpoint.sendData(ProtocolMessageCode.blockTransactionsChunk, mOut.ToArray());
                    }
                }
            }

            public static void handleGetTransaction(byte[] data, RemoteEndpoint endpoint)
            {
                if (Node.blockSync.synchronizing)
                {
                    return;
                }
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        // Retrieve the transaction id
                        string txid = reader.ReadString();
                        ulong block_num = reader.ReadUInt64();

                        Transaction transaction = null;

                        // Check for a transaction corresponding to this id
                        if (block_num == 0 || block_num == Node.blockChain.getLastBlockNum() + 1)
                        {
                            transaction = TransactionPool.getUnappliedTransaction(Transaction.txIdLegacyToV8(txid));
                        }
                        if (transaction == null)
                        {
                            transaction = TransactionPool.getAppliedTransaction(Transaction.txIdLegacyToV8(txid), block_num, true);
                        }

                        if (transaction == null)
                        {
                            Logging.warn("I do not have txid '{0}.", txid);
                            return;
                        }

                        Logging.info("Sending transaction {0} - {1} - {2}.", Transaction.txIdV8ToLegacy(transaction.id), Crypto.hashToString(transaction.checksum), transaction.amount);

                        endpoint.sendData(ProtocolMessageCode.transactionData, transaction.getBytes(true));
                    }
                }
            }

            public static void handleGetTransaction2(byte[] data, RemoteEndpoint endpoint)
            {
                if (Node.blockSync.synchronizing)
                {
                    return;
                }
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        // Retrieve the transaction id
                        int txid_len = (int)reader.ReadIxiVarUInt();
                        byte[] txid = reader.ReadBytes(txid_len);
                        ulong block_num = reader.ReadIxiVarUInt();

                        Transaction transaction = null;

                        string txid_str = UTF8Encoding.UTF8.GetString(txid);

                        // Check for a transaction corresponding to this id
                        if (block_num == 0 || block_num == Node.blockChain.getLastBlockNum() + 1)
                        {
                            transaction = TransactionPool.getUnappliedTransaction(Transaction.txIdLegacyToV8(txid_str));
                        }
                        if (transaction == null)
                        {
                            transaction = TransactionPool.getAppliedTransaction(Transaction.txIdLegacyToV8(txid_str), block_num, true);
                        }

                        if (transaction == null)
                        {
                            Logging.warn("I do not have txid '{0}.", Transaction.txIdV8ToLegacy(txid));
                            return;
                        }

                        Logging.info("Sending transaction {0} - {1} - {2}.", Transaction.txIdV8ToLegacy(transaction.id), Crypto.hashToString(transaction.checksum), transaction.amount);

                        endpoint.sendData(ProtocolMessageCode.transactionData, transaction.getBytes(true));
                    }
                }
            }

            public static void handleGetTransaction3(byte[] data, RemoteEndpoint endpoint)
            {
                if (Node.blockSync.synchronizing)
                {
                    return;
                }
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        // Retrieve the transaction id
                        int txid_len = (int)reader.ReadIxiVarUInt();
                        byte[] txid = reader.ReadBytes(txid_len);
                        ulong block_num = reader.ReadIxiVarUInt();

                        Transaction transaction = null;

                        // Check for a transaction corresponding to this id
                        if (block_num == 0 || block_num == Node.blockChain.getLastBlockNum() + 1)
                        {
                            transaction = TransactionPool.getUnappliedTransaction(txid);
                        }
                        if (transaction == null)
                        {
                            transaction = TransactionPool.getAppliedTransaction(txid, block_num, true);
                        }

                        if (transaction == null)
                        {
                            Logging.warn("I do not have txid '{0}.", Transaction.txIdV8ToLegacy(txid));
                            return;
                        }

                        Logging.info("Sending transaction {0} - {1} - {2}.", Transaction.txIdV8ToLegacy(transaction.id), Crypto.hashToString(transaction.checksum), transaction.amount);

                        endpoint.sendData(ProtocolMessageCode.transactionData, transaction.getBytes(true));
                    }
                }
            }

            public static void handleTransactionData(byte[] data, RemoteEndpoint endpoint)
            {
                /*if(TransactionPool.checkSocketTransactionLimits(socket) == true)
                {
                    // Throttled, ignore this transaction
                    return;
                }*/

                Transaction transaction = new Transaction(data);
                if (transaction == null)
                    return;

                bool no_broadcast = false;
                if (!Node.blockSync.synchronizing)
                {
                    if (transaction.type == (int)Transaction.Type.StakingReward)
                    {
                        // Skip received staking transactions if we're not synchronizing
                        return;
                    }
                }
                else
                {
                    no_broadcast = true;
                }

                // Add the transaction to the pool
                TransactionPool.addTransaction(transaction, no_broadcast, endpoint);
            }
        }
    }
}