﻿using System;
using Microsoft.SPOT.Hardware;
using uScoober.Hardware.Spi;

namespace uScoober.Hardware.Spot
{
    internal class SpotSpiBus : DisposableBase,
                              ISpiBus
    {
        private readonly SPI.SPI_module _module;
        private readonly SPI _spotSpi;

        private int _lastConfigHash;

        public SpotSpiBus(SPI.SPI_module module) {
            _module = module;
            _spotSpi = new SPI(new SPI.Configuration(Cpu.Pin.GPIO_NONE, true, 0, 0, true, true, 0, module));
        }

        public void Read(SpiDeviceConfig config, byte[] buffer) {
            ConfigureBusForDevice(config);
            _spotSpi.WriteRead(new byte[] {config.NoOpCommand}, buffer, 0);
        }

        public void Read(SpiDeviceConfig config, ushort[] buffer, ByteOrder byteOrder) {
            ConfigureBusForDevice(config);
            //todo validate byte order
            _spotSpi.WriteRead(new ushort[] {config.NoOpCommand},
                               buffer);
            throw new NotImplementedException();
        }

        public void Write(SpiDeviceConfig config, byte[] buffer) {
            ConfigureBusForDevice(config);
            _spotSpi.Write(buffer);
        }

        public void Write(SpiDeviceConfig config, ushort[] buffer, ByteOrder byteOrder) {
            ConfigureBusForDevice(config);
            //todo validate byte order
            _spotSpi.Write(buffer);
        }

        public void WriteRead(SpiDeviceConfig config, byte[] writeBuffer, byte[] readBuffer, int startReadingAtOffset = 0) {
            ConfigureBusForDevice(config);
            _spotSpi.WriteRead(writeBuffer, readBuffer, startReadingAtOffset);
        }

        public void WriteRead(SpiDeviceConfig config, ushort[] writeBuffer, ushort[] readBuffer, ByteOrder byteOrder, int startReadingAtOffset = 0) {
            ConfigureBusForDevice(config);
            //todo validate byte order
            _spotSpi.WriteRead(writeBuffer, readBuffer, startReadingAtOffset);
        }

        public void WriteRead(SpiDeviceConfig config,
                              byte[] writeBuffer,
                              int writeOffset,
                              int writeCount,
                              byte[] readBuffer,
                              int readOffset,
                              int readCount,
                              int startReadingAtOffset = 0) {
            ConfigureBusForDevice(config);
            _spotSpi.WriteRead(writeBuffer, writeOffset, writeCount, readBuffer, readOffset, readCount, startReadingAtOffset);
        }

        public void WriteRead(SpiDeviceConfig config,
                              ushort[] writeBuffer,
                              int writeOffset,
                              int writeCount,
                              ushort[] readBuffer,
                              int readOffset,
                              int readCount,
                              ByteOrder byteOrder,
                              int startReadingAtOffset = 0) {
            ConfigureBusForDevice(config);
            //todo validate byte order
            _spotSpi.WriteRead(writeBuffer, writeOffset, writeCount, readBuffer, readOffset, readCount, startReadingAtOffset);
        }

        private void ConfigureBusForDevice(SpiDeviceConfig config) {
            int newHash = config.GetHashCode();
            if (newHash == _lastConfigHash) {
                return;
            }
            //todo optomize better by building a simple registry table
            _spotSpi.Config = new SPI.Configuration(config.ChipSelect.Pin,
                                                    config.ChipSelect.ActiveState,
                                                    config.ChipSelect_SetupTime,
                                                    config.ChipSelect_HoldTime,
                                                    config.Clock_IdleState,
                                                    config.Clock_Edge,
                                                    config.Clock_RateKHz,
                                                    _module);
            //todo include feedback signal
            _lastConfigHash = newHash;
        }
    }
}