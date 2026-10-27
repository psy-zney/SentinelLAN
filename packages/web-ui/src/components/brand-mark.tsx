import Image from 'next/image';

export function BrandMark() {
  return <Image src={`${process.env.NEXT_PUBLIC_BASE_PATH ?? ''}/icon.svg`} alt="" width={32} height={32} unoptimized aria-hidden="true" />;
}
