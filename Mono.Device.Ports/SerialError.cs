//
// System.IO.Ports.SerialError.cs
//
// Authors:
//  Carlos Alberto Cortez (calberto.cortez@gmail.com)
//
// (c) Copyright 2006 Novell, Inc. (http://www.novell.com)
//

namespace Mono.IO.Ports
{
    public enum SerialError
    {
        RxOver = 1,
        Overrun = 2,
        RxParity = 4,
        Frame = 8,
        TxFull = 256
    }
}
