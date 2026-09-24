/**
 * Pure TypeScript QR Code Generator (Zero Dependencies)
 * Generates valid standard QR Code (Version 1-6, Byte mode, EC Level M/L) SVG elements.
 */

// Galois Field GF(256) exponential and logarithmic tables for Reed-Solomon
const EXP_TABLE = new Uint8Array(512);
const LOG_TABLE = new Uint8Array(256);

(() => {
  let x = 1;
  for (let i = 0; i < 255; i++) {
    EXP_TABLE[i] = x;
    LOG_TABLE[x] = i;
    x <<= 1;
    if (x & 0x100) {
      x ^= 0x11d; // QR Code field polynomial x^8 + x^4 + x^3 + x^2 + 1
    }
  }
  for (let i = 255; i < 512; i++) {
    EXP_TABLE[i] = EXP_TABLE[i - 255];
  }
})();

function gfMul(x: number, y: number): number {
  if (x === 0 || y === 0) return 0;
  return EXP_TABLE[LOG_TABLE[x] + LOG_TABLE[y]];
}

// Generate Reed-Solomon generator polynomial for degree n
function rsGeneratorPolynomial(degree: number): Uint8Array {
  let poly = new Uint8Array([1]);
  for (let i = 0; i < degree; i++) {
    const nextPoly = new Uint8Array(poly.length + 1);
    const factor = EXP_TABLE[i];
    for (let j = 0; j < poly.length; j++) {
      nextPoly[j] ^= gfMul(poly[j], factor);
      nextPoly[j + 1] ^= poly[j];
    }
    poly = nextPoly;
  }
  return poly;
}

// Calculate Reed-Solomon error correction codewords
function rsCalculateEcc(data: Uint8Array, eccCount: number): Uint8Array {
  const genPoly = rsGeneratorPolynomial(eccCount);
  const remainder = new Uint8Array(eccCount);

  for (let i = 0; i < data.length; i++) {
    const factor = data[i] ^ remainder[0];
    for (let j = 0; j < eccCount - 1; j++) {
      remainder[j] = remainder[j + 1] ^ gfMul(genPoly[j], factor);
    }
    remainder[eccCount - 1] = gfMul(genPoly[eccCount - 1], factor);
  }

  return remainder;
}

// Capacity table for Byte mode (Version 1 to 6, Level M)
// [totalCodewords, dataCodewords, eccCodewordsPerBlock, numBlocks]
const VERSION_SPECS: Record<number, [number, number, number, number]> = {
  1: [26, 16, 10, 1], // 21x21, max 14 bytes
  2: [44, 28, 16, 1], // 25x25, max 26 bytes
  3: [70, 44, 26, 1], // 29x29, max 42 bytes
  4: [100, 64, 18, 2], // 33x33, max 62 bytes (2 blocks of 32)
  5: [134, 86, 24, 2], // 37x37, max 84 bytes
  6: [172, 108, 16, 4], // 41x41, max 106 bytes
};

export function generateQrMatrix(text: string): boolean[][] {
  const encoder = new TextEncoder();
  const rawBytes = encoder.encode(text);

  // Pick smallest version that fits data
  let version = 1;
  while (version <= 6) {
    const [, dataCap] = VERSION_SPECS[version];
    // Byte mode overhead: 4 bits mode + 8 bits length = 12 bits = 1.5 bytes -> need dataCap >= rawBytes.length + 2
    if (dataCap >= rawBytes.length + 2) break;
    version++;
  }
  if (version > 6) version = 6;

  const [totalCodewords, dataCodewords, eccPerBlock, numBlocks] = VERSION_SPECS[version];
  const size = 17 + 4 * version;

  // Bit buffer
  const bitBuffer: number[] = [];
  function pushBits(val: number, bits: number) {
    for (let i = bits - 1; i >= 0; i--) {
      bitBuffer.push((val >> i) & 1);
    }
  }

  // 1. Mode indicator: Byte mode = 0100
  pushBits(0b0100, 4);

  // 2. Character count indicator (8 bits for v1-9 byte mode)
  pushBits(Math.min(rawBytes.length, dataCodewords - 2), 8);

  // 3. Data bytes
  for (let i = 0; i < rawBytes.length && bitBuffer.length < dataCodewords * 8; i++) {
    pushBits(rawBytes[i], 8);
  }

  // 4. Terminator bits (up to 4 zeroes)
  const maxBits = dataCodewords * 8;
  const termLen = Math.min(4, maxBits - bitBuffer.length);
  pushBits(0, termLen);

  // 5. Pad to byte boundary
  while (bitBuffer.length % 8 !== 0) {
    bitBuffer.push(0);
  }

  // 6. Pad codewords with alternating 0xEC and 0x11
  const padBytes = [0xec, 0x11];
  let padIdx = 0;
  while (bitBuffer.length < maxBits) {
    pushBits(padBytes[padIdx % 2], 8);
    padIdx++;
  }

  // Convert bit buffer to data codewords
  const dataBytes = new Uint8Array(dataCodewords);
  for (let i = 0; i < dataCodewords; i++) {
    let byteVal = 0;
    for (let b = 0; b < 8; b++) {
      byteVal = (byteVal << 1) | bitBuffer[i * 8 + b];
    }
    dataBytes[i] = byteVal;
  }

  // Split into blocks and compute ECC
  const blockSize = Math.floor(dataCodewords / numBlocks);
  const allBlocks: Uint8Array[] = [];
  const allEcc: Uint8Array[] = [];

  for (let b = 0; b < numBlocks; b++) {
    const start = b * blockSize;
    const end = b === numBlocks - 1 ? dataCodewords : (b + 1) * blockSize;
    const block = dataBytes.slice(start, end);
    allBlocks.push(block);
    allEcc.push(rsCalculateEcc(block, eccPerBlock));
  }

  // Interleave data and ECC codewords
  const finalCodewords = new Uint8Array(totalCodewords);
  let cwIdx = 0;
  const maxBlockLen = Math.max(...allBlocks.map((b) => b.length));

  for (let i = 0; i < maxBlockLen; i++) {
    for (let b = 0; b < numBlocks; b++) {
      if (i < allBlocks[b].length) {
        finalCodewords[cwIdx++] = allBlocks[b][i];
      }
    }
  }
  for (let i = 0; i < eccPerBlock; i++) {
    for (let b = 0; b < numBlocks; b++) {
      if (i < allEcc[b].length) {
        finalCodewords[cwIdx++] = allEcc[b][i];
      }
    }
  }

  // Initialize matrix and function patterns mask
  const matrix: boolean[][] = Array.from({ length: size }, () => Array(size).fill(false));
  const isFunction: boolean[][] = Array.from({ length: size }, () => Array(size).fill(false));

  function setFunctionModule(r: number, c: number, val: boolean) {
    if (r >= 0 && r < size && c >= 0 && c < size) {
      matrix[r][c] = val;
      isFunction[r][c] = true;
    }
  }

  // Place Finder Pattern (7x7) + Separator
  function placeFinder(startRow: number, startCol: number) {
    for (let r = -1; r <= 7; r++) {
      for (let c = -1; c <= 7; c++) {
        const row = startRow + r;
        const col = startCol + c;
        if (row < 0 || row >= size || col < 0 || col >= size) continue;

        if (r >= 0 && r <= 6 && c >= 0 && c <= 6) {
          const isBlack = r === 0 || r === 6 || c === 0 || c === 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4);
          setFunctionModule(row, col, isBlack);
        } else {
          setFunctionModule(row, col, false); // Separator
        }
      }
    }
  }

  placeFinder(0, 0);
  placeFinder(0, size - 7);
  placeFinder(size - 7, 0);

  // Timing patterns
  for (let i = 8; i < size - 8; i++) {
    setFunctionModule(6, i, i % 2 === 0);
    setFunctionModule(i, 6, i % 2 === 0);
  }

  // Alignment pattern for version >= 2
  if (version >= 2) {
    const alignPos: Record<number, number[]> = {
      2: [6, 18],
      3: [6, 22],
      4: [6, 26],
      5: [6, 30],
      6: [6, 34],
    };
    const coords = alignPos[version] || [];
    for (const r of coords) {
      for (const c of coords) {
        if (isFunction[r][c]) continue; // Skip if overlaps finder
        for (let dr = -2; dr <= 2; dr++) {
          for (let dc = -2; dc <= 2; dc++) {
            const isBorder = Math.abs(dr) === 2 || Math.abs(dc) === 2 || (dr === 0 && dc === 0);
            setFunctionModule(r + dr, c + dc, isBorder);
          }
        }
      }
    }
  }

  // Reserve format information area
  for (let i = 0; i < 9; i++) {
    if (!isFunction[8][i]) setFunctionModule(8, i, false);
    if (!isFunction[i][8]) setFunctionModule(i, 8, false);
    if (i < 8) {
      if (!isFunction[8][size - 1 - i]) setFunctionModule(8, size - 1 - i, false);
      if (!isFunction[size - 1 - i][8]) setFunctionModule(size - 1 - i, 8, false);
    }
  }
  // Dark module
  setFunctionModule(4 * version + 9, 8, true);

  // Place data bits in zigzag right-to-left
  let bitIndex = 0;
  const totalBits = totalCodewords * 8;
  let direction = -1; // upwards
  let col = size - 1;
  let row = size - 1;

  while (col > 0) {
    if (col === 6) col--; // Skip timing column

    while (row >= 0 && row < size) {
      for (let c = 0; c < 2; c++) {
        const curCol = col - c;
        if (!isFunction[row][curCol]) {
          let bit = false;
          if (bitIndex < totalBits) {
            const bytePos = Math.floor(bitIndex / 8);
            const bitPos = 7 - (bitIndex % 8);
            bit = ((finalCodewords[bytePos] >> bitPos) & 1) === 1;
            bitIndex++;
          }
          // Mask 0: (row + col) % 2 === 0
          const maskInvert = (row + curCol) % 2 === 0;
          matrix[row][curCol] = bit !== maskInvert;
        }
      }
      row += direction;
    }
    direction = -direction;
    row += direction;
    col -= 2;
  }

  // Format info: EC Level M (00) + Mask 0 (000) = 00000 -> BCH code = 101010000010010 ^ 101010000010010 = 0
  // Standard format bits for (M, Mask 0): 101010000010010
  const formatBits = [1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0];
  for (let i = 0; i < 15; i++) {
    const val = formatBits[i] === 1;
    // Top-left copy
    if (i <= 5) matrix[8][i] = val;
    else if (i === 6) matrix[8][7] = val;
    else if (i === 7) matrix[8][8] = val;
    else if (i === 8) matrix[7][8] = val;
    else matrix[14 - i][8] = val;

    // Second copy
    if (i < 7) matrix[size - 1 - i][8] = val;
    else matrix[8][size - 15 + i] = val;
  }

  return matrix;
}

export function generateQrSvgPath(matrix: boolean[][], cellSize = 8): { path: string; size: number } {
  const count = matrix.length;
  const size = count * cellSize;
  let path = "";

  for (let r = 0; r < count; r++) {
    for (let c = 0; c < count; c++) {
      if (matrix[r][c]) {
        path += `M${c * cellSize},${r * cellSize}h${cellSize}v${cellSize}h-${cellSize}z `;
      }
    }
  }

  return { path, size };
}
