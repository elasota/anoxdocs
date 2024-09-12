import sys

def extract_chunk(f, header_line, out_path):
    zero_ord = ord("0")
    lowercase_a_ord = ord("a")
    uppercase_a_ord = ord("A")
    dot_ord = ord(".")
    slash_ord = ord("/")
    
    chunk_size = int(header_line[6:14], 16)
    chunk_padded_size = int(header_line[15:23], 16)

    all_bytes = bytearray()
    
    with open(out_path, "wb") as out_f:
        padded_size_remaining = chunk_padded_size

        misaligned_trailing = padded_size_remaining % 3
        prefix_zeroes = 0
        if misaligned_trailing != 0:
            prefix_zeroes = (3 - misaligned_trailing)
            padded_size_remaining += prefix_zeroes

        while padded_size_remaining > 0:
            line_str = f.readline()

            if (len(line_str) == 0):
                raise RuntimeError("Data terminated prematurely")
                return

            if line_str[0] == '&':
                line_str = line_str[1:].rstrip()
                if len(line_str) % 4 != 0:
                    raise RuntimeError("Invalid data line")
                num_bytes_in_line = len(line_str) / 4

                value_accum = 0
                for i in range(0, len(line_str)):
                    char_ord = ord(line_str[i])
                    char_sym = 0
                    if char_ord >= zero_ord and char_ord < (zero_ord + 10):
                        char_sym = 0 + (char_ord - zero_ord)
                    elif char_ord >= uppercase_a_ord and char_ord < (uppercase_a_ord + 26):
                        char_sym = 10 + (char_ord - uppercase_a_ord)
                    elif char_ord >= lowercase_a_ord and char_ord < (lowercase_a_ord + 26):
                        char_sym = 36 + (char_ord - lowercase_a_ord)
                    elif char_ord == dot_ord:
                        char_sym = 62
                    elif char_ord == slash_ord:
                        char_sym = 63
                    else:
                        raise RuntimeError("Damaged line str char '" + line_str[i] + "', ord was " + str(char_ord))

                    value_accum = value_accum * 64 + char_sym

                    if i % 4 == 3:
                        all_bytes.append((value_accum // 256 // 256) % 256)
                        all_bytes.append((value_accum // 256) % 256)
                        all_bytes.append(value_accum % 256)

                        value_accum = 0
                        padded_size_remaining = padded_size_remaining - 3

        all_bytes = all_bytes[prefix_zeroes:]
        all_bytes = all_bytes[:chunk_size]

        out_f.write(all_bytes)

in_path = sys.argv[1]
chunk_type_to_extract = sys.argv[2]
out_path = sys.argv[3]

with open(in_path, "r") as f:
    while True:
        line_str = f.readline()
        if len(line_str) == 0:
               break

        if line_str[0] == '$' and len(line_str) >= 5:
            chunk_type = line_str[1:5]
            if chunk_type == chunk_type_to_extract:
                extract_chunk(f, line_str, out_path)
                break
